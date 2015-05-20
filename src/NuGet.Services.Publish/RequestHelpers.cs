using System;
using System.IO;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Publish
{
    public static class RequestHelpers
    {
        public static bool TryReadBody(IOwinContext context, out JObject body)
        {
            try
            {
                using (StreamReader reader = new StreamReader(context.Request.Body))
                {
                    JObject obj = JObject.Parse(reader.ReadToEnd());
                    body = obj;
                    return true;
                }
            }
            catch (FormatException)
            {
                body = null;
                return false;
            }
        }
    }
}