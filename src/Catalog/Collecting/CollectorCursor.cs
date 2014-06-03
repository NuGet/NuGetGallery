using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class CollectorCursor
    {
        private string _val;

        public CollectorCursor(DateTime datetime)
        {
            _val = datetime.ToString();
        }

        public static explicit operator DateTime(CollectorCursor cur)
        {
            return DateTime.Parse(cur._val);
        }

        public static implicit operator CollectorCursor(DateTime datetime)
        {
            return new CollectorCursor(datetime);
        }
    }
}
