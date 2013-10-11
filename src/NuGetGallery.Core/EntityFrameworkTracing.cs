using System;
using System.Collections.Generic;
using System.Data.Objects;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public static class EntityFrameworkTracing
    {
        public static string ToTraceString(object query)
        {
            ObjectQuery objectQuery = GetObjectQuery(query);
            return ToTraceStringWithParameters(objectQuery);
        }

        private static ObjectQuery GetObjectQuery(object query)
        {
            object internalQuery = GetFieldValue(query, "_internalQuery");
            if (internalQuery == null)
            {
                return null;
            }
            object objectQuery = GetFieldValue(internalQuery, "_objectQuery");
            return objectQuery as ObjectQuery;
        }

        private static object GetFieldValue(object obj, string name)
        {
            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.Name == name)
                {
                    return field.GetValue(obj);
                }
            }
            return null;
        }

        private static string ToTraceStringWithParameters(ObjectQuery query)
        {
            if (query == null)
            {
                return "(null)";
            }
            string traceString = query.ToTraceString() + "\n";
            foreach (ObjectParameter parameter in query.Parameters)
            {
                traceString += "    " + parameter.Name + " [" + parameter.ParameterType.FullName + "] = " + parameter.Value + "\n";
            }
            return traceString;
        }
    }
}
