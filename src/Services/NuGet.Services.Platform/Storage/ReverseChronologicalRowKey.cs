using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Services.Storage
{
    public static class ReverseChronologicalRowKey
    {
        public static string Create(DateTimeOffset timestamp)
        {
            return Create(timestamp, null);
        }

        public static string Create(DateTimeOffset timestamp, string uniqueId)
        {
            var stamp = (DateTimeOffset.MaxValue.UtcTicks - timestamp.UtcTicks).ToString("d19");
            if (!String.IsNullOrEmpty(uniqueId))
            {
                return stamp + "_" + uniqueId;
            }
            return stamp;
        }
    }
}
