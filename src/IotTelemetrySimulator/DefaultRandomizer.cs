namespace IotTelemetrySimulator
{
    using System;
    using System.Threading;

    public class DefaultRandomizer : IRandomizer
    {
        private readonly ThreadLocal<Random> generator
            = new ThreadLocal<Random>(() => new Random());

        public int Next()
        {
            return this.generator.Value.Next();
        }

        public int Next(int max)
        {
            return this.generator.Value.Next(max);
        }

        public int Next(int min, int max)
        {
            return this.generator.Value.Next(min, max);
        }

        public double NextDouble()
        {
            return this.generator.Value.NextDouble();
        }

        public double NextDouble(double min, double max)
        {
            return this.generator.Value.NextDouble() * (max - min) + min;
        }

        // Implementation https://stackoverflow.com/a/218600
        public double NextBoxMullerDouble(double mean = 0, double std = 1)
        {
            // uniform(0,1] random doubles
            double u1 = 1.0 - this.generator.Value.NextDouble();
            double u2 = 1.0 - this.generator.Value.NextDouble();
            // random normal(0,1)
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            // random normal(mean,stdDev^2)
            double randNormal = mean + std * randStdNormal;
            return randNormal;
        }
    }
}
