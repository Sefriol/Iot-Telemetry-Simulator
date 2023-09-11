﻿namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Scripting;
    using Microsoft.CodeAnalysis.Scripting;

    public class TelemetryValues
    {
        private readonly IRandomizer random = new DefaultRandomizer();
        readonly string machineName;

        public IList<TelemetryVariable> Variables { get; }

        public TelemetryValues(IList<TelemetryVariable> variables)
        {
            this.Variables = variables;
            this.machineName = Environment.MachineName;
        }

        public ExpandoObject NextValues(ExpandoObject previous)
        {
            dynamic globals = new ExpandoObject();
            var prev = (IDictionary<string, object>)previous;
            var next = (IDictionary<string, object>)globals;
            var now = DateTime.Now;
            var dynamics = new Dictionary<string, TelemetryVariable>();

            ulong iterationNumber = 0;
            if (previous != null && prev.TryGetValue(Constants.IterationNumberValueName, out var previousIterationNumber))
            {
                iterationNumber = (ulong)previousIterationNumber + 1;
            }

            next[Constants.TimeValueName] = now.ToUniversalTime().ToString("o");
            next[Constants.LocalTimeValueName] = now.ToString("o");
            next[Constants.TicksValueName] = now.Ticks;
            next[Constants.EpochValueName] = new DateTimeOffset(now).ToUnixTimeSeconds();
            next[Constants.GuidValueName] = Guid.NewGuid().ToString();
            next[Constants.MachineNameValueName] = this.machineName;
            next[Constants.IterationNumberValueName] = iterationNumber;

            if (previous != null)
            {
                next[Constants.DeviceIdValueName] = prev[Constants.DeviceIdValueName];
            }

            var hasSequenceVars = false;

            foreach (var val in this.Variables)
            {
                if (val.Sequence)
                {
                    hasSequenceVars = true;
                }
                else if (val.Random)
                {
                    if (val.Min.HasValue && val.Max.HasValue && val.Max > val.Min)
                    {
                        next[val.Name] = this.random.Next((int)val.Min.Value, (int)val.Max.Value);
                    }
                    else
                    {
                        next[val.Name] = this.random.Next();
                    }
                }
                else if (val.RandomDouble)
                {
                    if (val.Min.HasValue && val.Max.HasValue && val.Max > val.Min)
                    {
                        next[val.Name] = this.random.NextDouble(val.Min.Value, val.Max.Value);
                    }
                    else
                    {
                        next[val.Name] = this.random.NextDouble();
                    }
                }
                else if (val.RandomBoxMuller)
                {
                    next[val.Name] = this.random.NextBoxMullerDouble(val.Mean.Value, val.Std.Value);
                }
                else if (val.Evaluate != null)
                {
                    dynamics[val.Name] = val;
                }
                else if (val.CustomLengthString != null)
                {
                    next[val.Name] = this.CreateRandomString(val.CustomLengthString.Value);
                }
                else if (val.Values != null && val.Values.Length > 0)
                {
                    next[val.Name] = val.Values[this.random.Next(val.Values.Length)];
                }
                else
                {
                    if (previous != null && prev.TryGetValue(val.Name, out var prevValue))
                    {
                        var step = val.Step ?? 1.0;
                        var maxThres = val.Max ?? double.MaxValue;

                        switch (prevValue)
                        {
                            case double preDValue when preDValue > maxThres - step:
                                next[val.Name] = val.Min == null ? 1 : (double)val.Min;
                                break;

                            case double preDValue:
                                next[val.Name] = preDValue + step;
                                break;
                        }
                    }
                    else
                    {
                        next[val.Name] = val.Min ?? 1;
                    }
                }
            }

            var refs = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).GetTypeInfo().Assembly.Location)
            };

            foreach (var (key, val) in dynamics)
            {
                var script = CSharpScript.Create(val.Evaluate, options: ScriptOptions.Default.AddReferences(refs), globalsType: typeof(ArgsWrapper));
                if (!prev.ContainsKey(key))
                {
                    prev.Add(key, val.Min ?? 0);
                }

                var g = new ArgsWrapper { Variables = globals, Previous = previous };
                next[key] = script.RunAsync(g).Result.ReturnValue;
            }

            // We generate values of sequence vars after the non-sequence vars, because
            // sequence vars might reference non-sequence vars.
            if (hasSequenceVars)
            {
                var notUsedSequenceVariables = this.Variables
                                                .Where(x => x.Sequence)
                                                .SelectMany(x => x.GetReferenceVariableNames())
                                                .ToHashSet();

                foreach (var seqVar in this.Variables.Where(x => x.Sequence))
                {
                    var value = seqVar.Values[iterationNumber % (ulong)seqVar.Values.Length];
                    string usedVariable = null;
                    if (value is string valueString && valueString.StartsWith("$."))
                    {
                        usedVariable = valueString[2..];
                        if (next.TryGetValue(usedVariable, out var valueFromVariable))
                        {
                            next[seqVar.Name] = valueFromVariable;
                            notUsedSequenceVariables.Remove(usedVariable);
                        }
                        else
                        {
                            next[seqVar.Name] = value;
                        }
                    }
                    else
                    {
                        next[seqVar.Name] = value;
                    }
                }

                ResetNotUsedReferencedVariables(previous, next, notUsedSequenceVariables);
            }

            return globals;
        }

        /// <summary>
        /// Removes non-used variables in a sequence.
        /// This way we can keep the a counter variable incrementally correctly if the sequence did not use it in current iteration.
        /// </summary>
        private static void ResetNotUsedReferencedVariables(
            IDictionary<string, object> previous,
            IDictionary<string, object> next,
            IEnumerable<string> notUsedVariables)
        {
            foreach (var notUsedVariable in notUsedVariables)
            {
                // Restore it from the previous value.
                if (previous != null && previous.TryGetValue(notUsedVariable, out var previousValue))
                {
                    next[notUsedVariable] = previousValue;
                }
                else
                {
                    next.Remove(notUsedVariable);
                }
            }
        }

        /// <summary>
        /// All possible variable names this object can produce.
        /// </summary>
        public IEnumerable<string> VariableNames()
        {
            return this.Variables.Select(v => v.Name).Concat(Constants.AllSpecialValueNames);
        }

        public string CreateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[this.random.Next(s.Length)]).ToArray());
        }
    }
}
