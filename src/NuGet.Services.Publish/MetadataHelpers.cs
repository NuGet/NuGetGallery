using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Services.Publish
{
    public static class MetadataHelpers
    {
        public static string GetName(Uri type, string vocab)
        {
            string strType = type.AbsoluteUri;

            if (strType.StartsWith(vocab))
            {
                strType = strType.Substring(vocab.Length);
            }

            return strType;
        }

        public static bool IsType(JObject obj, string type)
        {
            JToken token;
            if (obj.TryGetValue("@type", out token))
            {
                if (token.Type == JTokenType.Array)
                {
                    foreach (string t in (JArray)token)
                    {
                        if (t == type)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    return (obj["@type"].ToString() == type);
                }
            }

            return false;
        }

        public static void AssertType(JObject obj, string type)
        {
            JToken token;
            if (obj.TryGetValue("@type", out token))
            {
                if (token.Type == JTokenType.Array)
                {
                    ((JArray)token).Add(type);
                }
                else
                {
                    obj["@type"] = new JArray { token.ToString(), type };
                }
            }
            else
            {
                obj["@type"] = type;
            }
        }

        public static void AssertType(JObject obj, Uri type, string vocab)
        {
            AssertType(obj, GetName(type, vocab));
        }

        public static string ContentTypeFromExtension(string name)
        {
            string DefaultContentType = "application/octet-stream";

            IDictionary<string, string> ContentTypes = new Dictionary<string, string>
            {
                { "png", "image/png" },
                { "json", "application/json" },
                { "exe", "application/octet-stream" },
                { "dll", "application/octet-stream" },
                { "xml", "text/xml" },
            };

            int dot = name.LastIndexOf('.');

            if (dot == -1 || dot + 1 == name.Length)
            {
                return DefaultContentType;
            }

            string extension = name.Substring(dot + 1);

            string contentType;
            if (ContentTypes.TryGetValue(extension, out contentType))
            {
                return contentType;
            }

            return DefaultContentType;
        }
    }
}