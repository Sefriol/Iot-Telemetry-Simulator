namespace IotTelemetrySimulator.Test
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using Xunit;

    public class TemplateVariableSubstitutionTest
    {
        private class CustomClass
        {
            private readonly string message;

            public CustomClass(string message)
            {
                this.message = message;
            }

            public override string ToString()
            {
                return this.message;
            }
        }

        [Fact]
        public void ShouldSubstituteSpecialVariables()
        {
            var variables = new TelemetryValues(new[]
            {
                new TelemetryVariable
                {
                    Min = 1,
                    Name = "Counter",
                    Step = 1,
                },
            });

            var telemetryTemplate = new TelemetryTemplate(
                $"$.Counter $.{Constants.TicksValueName} $.{Constants.GuidValueName}",
                variables.VariableNames());

            var generatedPayload = telemetryTemplate.Create(
                variables.NextValues(previous: null));

            var parts = generatedPayload.Split();

            Assert.Equal(3, parts.Length);
            Assert.Equal("1", parts[0]); // Counter must be 1
            Assert.True(parts[1].All(char.IsDigit));  // Ticks must be an int
            Assert.True(Guid.TryParse(parts[2], out _));  // Guid should be parseable
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void ShouldSubstituteVariablesCorrectly(
            ExpandoObject previousValues,
            ExpandoObject vars,
            string template,
            string expectedPayload)
        {
            var variables = new TelemetryValues(
                vars.Select(var => new TelemetryVariable
                {
                    Name = var.Key,
                    Values = new[] { var.Value },
                }).ToArray());

            var telemetryTemplate = new TelemetryTemplate(
                template,
                variables.VariableNames());

            var generatedPayload = telemetryTemplate.Create(
                variables.NextValues(previous: previousValues));

            Assert.Equal(expectedPayload, generatedPayload);
        }

        public static IEnumerable<object[]> GetTestData()
        {
            // Should convert variable values to string
            var obj1 = new ExpandoObject();
            var dict1 = (IDictionary<string, object>)obj1;
            var customObj = new CustomClass("some_data");
            dict1.Add("name", "World");
            dict1.Add("var1", customObj);
            dict1.Add("var2", -0.5);
            dict1.Add("var3", string.Empty);
            yield return new object[]
            {
                null,
                obj1,
                "Hello, $.name! I like $.var1, $.var2 and $.var3, $.name.",
                $"Hello, World! I like some_data, -0.5 and , World.",
            };

            // Should ignore extra variables
            var obj2 = new ExpandoObject();
            var dict2 = (IDictionary<string, object>)obj2;
            dict2.Add("var1", 1);
            dict2.Add("var2", 2);
            yield return new object[]
            {
                null,
                obj2,
                "Only $.var1",
                "Only 1",
            };

            // Should allow empty variables
            yield return new object[]
            {
                null,
                new ExpandoObject(),
                "$.something",
                "$.something",
            };

            // Should ignore non-existent variables in the template
            var obj3 = new ExpandoObject();
            var dict3 = (IDictionary<string, object>)obj3;
            dict3.Add("var1", 1);
            yield return new object[]
            {
                null,
                obj3,
                "$.var1 $.var",
                "1 $.var",
            };

            // ..even with the special name
            var obj4 = new ExpandoObject();
            var dict4 = (IDictionary<string, object>)obj4;
            dict4.Add("var1", 1);
            yield return new object[]
            {
                null,
                obj4,
                $"$.{Constants.DeviceIdValueName} $.var1 $.var",
                $"$.{Constants.DeviceIdValueName} 1 $.var",
            };

            // Should substitute longer names first
            var obj5 = new ExpandoObject();
            var dict5 = (IDictionary<string, object>)obj5;
            dict5.Add("var1", 1);
            dict5.Add("var", 2);
            dict5.Add("var11", 3);
            yield return new object[]
            {
                null,
                obj5,
                "$.var1$.var11, $.var $.var$.var1$.var11!",
                "13, 2 213!",
            };

            // Should substitute DeviceID if it's in the previous values
            var objd = new ExpandoObject();
            var dictd = (IDictionary<string, object>)objd;
            dictd.Add(Constants.DeviceIdValueName, "dummy");
            var obj6 = new ExpandoObject();
            var dict6 = (IDictionary<string, object>)obj6;
            dict6.Add("var1", 1);
            yield return new object[]
            {
                dictd,
                obj6,
                $"$.{Constants.DeviceIdValueName} $.var1!",
                "dummy 1!",
            };
        }
    }
}
