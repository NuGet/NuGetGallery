// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Services.BasicSearch
{
    public class ResponseWriter
    {
        public async Task WriteResponseAsync(IOwinContext context, HttpStatusCode statusCode, JToken content)
        {
            await WriteResponseAsync(context, statusCode, w => context.Response.Write(content.ToString()));
        }

        public async Task WriteResponseAsync(IOwinContext context, HttpStatusCode statusCode, Action<JsonWriter> writeContent)
        {
            using (var content = new MemoryStream())
            {
                WriteToStream(content, writeContent);
                content.Position = 0;
                
                // write the response
                context.Response.StatusCode = (int)statusCode;
                context.Response.Headers.Add("Pragma", new[] { "no-cache" });
                context.Response.Headers.Add("Cache-Control", new[] { "no-cache" });
                context.Response.Headers.Add("Expires", new[] { "0" });

                var callback = context.Request.Query["callback"];
                if (string.IsNullOrEmpty(callback))
                {
                    context.Response.ContentType = "application/json";
                    await content.CopyToAsync(context.Response.Body);
                }
                else
                {
                    context.Response.ContentType = "application/javascript";
                    await context.Response.WriteAsync($"{callback}(");
                    await content.CopyToAsync(context.Response.Body);
                    await context.Response.WriteAsync(")");
                }
            }
        }

        public async Task WriteResponseAsync(IOwinContext context, ClientException e)
        {
            await WriteResponseAsync(context, e.StatusCode, e.Content);
        }

        public async Task WriteResponseAsync(IOwinContext context, Exception e, FrameworkLogger logger)
        {
            logger.LogError("Internal server error: {0}", e);

            await WriteResponseAsync(context, HttpStatusCode.InternalServerError, JObject.FromObject(new
            {
                error = "Internal server error",
                httpRequestId = context.Get<string>(CorrelationIdMiddleware.CorrelationIdHeaderKey)
            }));
        }

        private void WriteToStream(Stream destination, Action<JsonWriter> writeContent)
        {
            using (var streamWriter = new StreamWriter(destination, new UTF8Encoding(false), 4096, true))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                writeContent(jsonWriter);

                jsonWriter.Flush();
                streamWriter.Flush();
            }
        }
    }
}