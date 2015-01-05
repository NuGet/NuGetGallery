using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace NuGet.Services.Publish
{
    public static class ServiceHelpers
    {
        public static async Task WriteResponse(IOwinContext context, JToken content, HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;

            if (content != null)
            {
                string callback = context.Request.Query["callback"];

                string contentType;
                string responseString;
                if (string.IsNullOrEmpty(callback))
                {
                    responseString = content.ToString();
                    contentType = "application/json";
                }
                else
                {
                    responseString = string.Format("{0}({1})", callback, content);
                    contentType = "application/javascript";
                }

                context.Response.Headers.Add("Pragma", new string[] { "no-cache" });
                context.Response.Headers.Add("Cache-Control", new string[] { "no-cache" });
                context.Response.Headers.Add("Expires", new string[] { "0" });
                context.Response.ContentType = contentType;

                await context.Response.WriteAsync(responseString);
            }
        }
    }
}