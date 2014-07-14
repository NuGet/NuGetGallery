using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class CollectorCursor
    {
        public string Value { get; private set; }

        public CollectorCursor(string value)
        {
            Value = value;
        }
        public CollectorCursor(DateTime datetime) : this(datetime.ToString("O"))
        {
        }

        public static explicit operator DateTime(CollectorCursor cur)
        {
            return DateTime.Parse(cur.Value).ToUniversalTime();
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
    }
}
