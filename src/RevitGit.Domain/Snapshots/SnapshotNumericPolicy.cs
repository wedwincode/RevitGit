using System;
using RevitGit.Domain.Exceptions;

namespace RevitGit.Domain.Snapshots
{
    public static class SnapshotNumericPolicy
    {
        public const int DecimalPlaces = 6;
        public const string NumberUnit = "unitless";
        public const string LengthUnit = "millimeter";
        public const string AreaUnit = "square millimeter";
        public const string VolumeUnit = "cubic millimeter";
        public const string AngleUnit = "degree";

        public static double Normalize(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new DomainException("Snapshot numeric values must be finite.");
            }

            var rounded = Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);
            return rounded == 0d ? 0d : rounded;
        }
    }
}
