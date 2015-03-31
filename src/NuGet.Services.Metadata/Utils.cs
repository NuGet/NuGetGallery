using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace NuGet.Services.Metadata
{
    //TODO: we need a home for common code across the Publish and Metadata services

    public static class Utils
    {
        public static async Task WriteErrorResponse(IOwinContext context, string error, HttpStatusCode statusCode)
        {
            JToken content = new JObject 
            {
                { "type", "SimpleError" },
                { "error", error }
            };

            await WriteResponse(context, content, statusCode);
        }

        public static async Task WriteErrorResponse(IOwinContext context, IEnumerable<string> errors, HttpStatusCode statusCode)
        {
            JArray array = new JArray();
            foreach (string error in errors)
            {
                array.Add(error);
            }

            JToken content = new JObject
            { 
                { "type", "ValidationError" },
                { "errors", array }
            };

            await WriteResponse(context, content, statusCode);
        }

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