﻿namespace IotTelemetrySimulator
{
    using System.Dynamic;

    public class FixPayload : PayloadBase
    {
        public byte[] Payload { get; }

        public FixPayload(int distribution, byte[] payload)
            : this(distribution, null, payload)
        {
        }

        public FixPayload(int distribution, string deviceId, byte[] payload)
            : base(distribution, deviceId)
        {
            this.Payload = payload;
        }

        public override (byte[], ExpandoObject) Generate(ExpandoObject variableValues)
        {
            return (this.Payload, variableValues);
        }

        public override string GetDescription()
        {
            return $"Fix: {this.Payload.Length} bytes";
        }
    }
}
