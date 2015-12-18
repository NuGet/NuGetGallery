using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Services.BasicSearch
{
    public static class ResponseHelpers
    {
        public static async Task WriteResponseAsync(IOwinContext context, HttpStatusCode statusCode, JToken content)
        {
            await WriteResponseAsync(context, statusCode, w => context.Response.Write(content.ToString()));
        }

        public static async Task WriteResponseAsync(IOwinContext context, HttpStatusCode statusCode, Action<JsonWriter> writeContent)
        {
            /*
             * If an exception is thrown when building the response body, the exception must be handled before returning
             * a 200 OK (and presumably return a 500 Internal Server Error). If this approach becomes a memory problem,
             * we can write directly to the response stream. However, we must be sure to a) have proper validation to
             * avoid service exceptions (which is never a sure thing) or b) be okay with returning 200 OK on service
             * exceptions. Another approach could also separate "do business logic" with "build response body". Since
             * most exceptions will likely happen during the "do business logic" step, this would reduce the change of
             * a 200 OK on service exception. However, this means that the whole result of the business logic is in
             * memory.
             */
            var content = new MemoryStream();
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

        public static async Task WriteResponseAsync(IOwinContext context, ClientException e)
        {
            await WriteResponseAsync(context, e.StatusCode, e.Content);
        }

        public static async Task WriteResponseAsync(IOwinContext context, Exception e, FrameworkLogger logger)
        {
            ServiceHelpers.TraceException(e, logger);
            await WriteResponseAsync(context, HttpStatusCode.InternalServerError, JObject.FromObject(new { error = "Internal server error" }));
        }

        private static void WriteToStream(Stream destination, Action<JsonWriter> writeContent)
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