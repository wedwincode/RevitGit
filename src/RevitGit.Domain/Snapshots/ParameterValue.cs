using System;
using RevitGit.Domain.Exceptions;

namespace RevitGit.Domain.Snapshots
{
    public sealed class ParameterValue : IEquatable<ParameterValue>
    {
        private ParameterValue(
            ParameterValueKind kind,
            string stringValue,
            long? integerValue,
            double? doubleValue,
            bool? booleanValue)
        {
            Kind = kind;
            StringValue = stringValue;
            IntegerValue = integerValue;
            DoubleValue = doubleValue;
            BooleanValue = booleanValue;
        }

        public static ParameterValue Null { get; } =
            new ParameterValue(ParameterValueKind.Null, null, null, null, null);

        public ParameterValueKind Kind { get; }

        public string StringValue { get; }

        public long? IntegerValue { get; }

        public double? DoubleValue { get; }

        public bool? BooleanValue { get; }

        public static ParameterValue FromString(string value)
        {
            if (value == null)
            {
                throw new DomainException("A string parameter value cannot be null. Use ParameterValue.Null instead.");
            }

            return new ParameterValue(ParameterValueKind.String, value, null, null, null);
        }

        public static ParameterValue FromInteger(long value)
        {
            return new ParameterValue(ParameterValueKind.Integer, null, value, null, null);
        }

        public static ParameterValue FromDouble(double value)
        {
            return new ParameterValue(
                ParameterValueKind.Double,
                null,
                null,
                SnapshotNumericPolicy.Normalize(value),
                null);
        }

        public static ParameterValue FromBoolean(bool value)
        {
            return new ParameterValue(ParameterValueKind.Boolean, null, null, null, value);
        }

        public bool Equals(ParameterValue other)
        {
            return other != null
                   && Kind == other.Kind
                   && string.Equals(StringValue, other.StringValue, StringComparison.Ordinal)
                   && IntegerValue.Equals(other.IntegerValue)
                   && DoubleValue.Equals(other.DoubleValue)
                   && BooleanValue.Equals(other.BooleanValue);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ParameterValue);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ (StringValue == null ? 0 : StringComparer.Ordinal.GetHashCode(StringValue));
                hashCode = (hashCode * 397) ^ IntegerValue.GetHashCode();
                hashCode = (hashCode * 397) ^ DoubleValue.GetHashCode();
                hashCode = (hashCode * 397) ^ BooleanValue.GetHashCode();
                return hashCode;
            }
        }
    }
}
