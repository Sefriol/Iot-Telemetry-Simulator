﻿namespace IotTelemetrySimulator
{
    using System.Dynamic;
    using System.Text;

    public class TemplatedPayload : PayloadBase
    {
        public TelemetryTemplate Template { get; }

        public TelemetryValues Variables { get; }

        public TemplatedPayload(int distribution, TelemetryTemplate template, TelemetryValues variables)
            : this(distribution, null, template, variables)
        {
        }

        public TemplatedPayload(int distribution, string deviceId, TelemetryTemplate template, TelemetryValues variables)
            : base(distribution, deviceId)
        {
            this.Template = template;
            this.Variables = variables;
        }

        public override (byte[], ExpandoObject) Generate(ExpandoObject variableValues)
        {
            var nextVariables = this.Variables.NextValues(variableValues);
            var data = this.Template.Create(nextVariables);
            return (Encoding.UTF8.GetBytes(data), nextVariables);
        }

        public override string GetDescription()
        {
            return $"Template: {this.Template.ToString()}";
        }
    }
}
