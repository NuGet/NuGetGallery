using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PublishTestDriverWebSite.Utils
{
    public static class JsonUtils
    {
        public static string Get(JObject obj, string name)
        {
            JToken token;
            if (obj.TryGetValue(name, out token))
            {
                return token.ToString();
            }
            return string.Empty;
        }

        public static bool GetBool(JObject obj, string name)
        {
            JToken token;
            if (obj.TryGetValue(name, out token))
            {
                bool result;
                if (bool.TryParse(token.ToString(), out result))
                {
                    return result;
                }
            }
            return false;
        }

        public static List<string> GetList(JObject obj, string name)
        {
            List<string> result = new List<string>();
            JToken token;
            if (obj.TryGetValue(name, out token))
            {
                foreach (JToken item in (JArray)token)
                {
                    result.Add(item.ToString());
                }
            }
            return result;
        }
    }
}