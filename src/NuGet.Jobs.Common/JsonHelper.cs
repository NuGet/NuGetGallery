using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace NuGet.Jobs.Common
{
    public static class JsonHelper
    {
        public static JArray MakeJArray(IDictionary<string, HashSet<string>> data)
        {
            JArray result = new JArray();
            foreach (var entry in data)
            {
                result.Add(new JArray(entry.Key, new JArray(entry.Value.ToArray())));
            }
            return result;
        }

        public static JArray SqlDataReader2Json(SqlDataReader reader, string col0, string col1)
        {
            var colNames = new Dictionary<string, int>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                colNames[reader.GetName(i)] = i;
            }

            var parent = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                string parentColumn = reader.GetString(colNames[col0]);
                string childColumn = reader.GetString(colNames[col1]);

                HashSet<string> child;
                if (!parent.TryGetValue(parentColumn, out child))
                {
                    child = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    parent.Add(parentColumn, child);
                }

                child.Add(childColumn);
            }

            return MakeJArray(parent);
        }
    }
}
