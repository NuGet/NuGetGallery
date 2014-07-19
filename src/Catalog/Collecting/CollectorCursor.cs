using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class CollectorCursor : IComparable<DateTime>, IComparable<CollectorCursor>
    {
        /// <summary>
        /// Cursor value that is guaranteed to compare as lower than all other cursor values. Can be used as an
        /// initial value for a cursor.
        /// </summary>
        public static readonly CollectorCursor None = DateTime.MinValue.ToUniversalTime();

        private DateTime _rawValue;

        public string Value { get { return _rawValue.ToString("O"); } }

        public CollectorCursor(DateTime rawValue)
        {
            _rawValue = rawValue.ToUniversalTime();
        }

        public static CollectorCursor FromString(string str)
        {
            return DateTime.Parse(str, CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        public static explicit operator DateTime(CollectorCursor cur)
        {
            return cur._rawValue;
        }

        public static implicit operator CollectorCursor(DateTime datetime)
        {
            return new CollectorCursor(datetime);
        }

        public override string ToString()
        {
            return Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            CollectorCursor other = obj as CollectorCursor;
            return other != null && String.Equals(other.Value, Value, StringComparison.Ordinal);
        }

        public int CompareTo(CollectorCursor other)
        {
            return _rawValue.CompareTo(other._rawValue);
        }

        public int CompareTo(DateTime other)
        {
            return _rawValue.CompareTo(other);
        }

        public static bool operator >(CollectorCursor left, CollectorCursor right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <(CollectorCursor left, CollectorCursor right)
        {
            return left.CompareTo(right) < 0;
        }
    }
}
