using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public static class JTokenExtensions
    {
        public static JArray GetJArray(this JToken token, string key)
        {
            var array = token[key];
            if (array == null)
            {
                return new JArray();
            }

            if (!(array is JArray))
            {
                array = new JArray(array);
            }

            return (JArray)array;
        }
    }
}