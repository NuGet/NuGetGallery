// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Microsoft.Owin;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public static class ServiceHelpers
    {
        public static Task WriteResponse(IOwinContext context, HttpStatusCode statusCode, JToken content)
        {
            return WriteResponse(context, statusCode, content.ToString());
        }

        public static Task WriteResponse(IOwinContext context, HttpStatusCode statusCode, string content)
        {
            string callback = context.Request.Query["callback"];

            string contentType;
            string responseString;
            if (string.IsNullOrEmpty(callback))
            {
                responseString = content;
                contentType = "application/json";
            }
            else
            {
                responseString = string.Format("{0}({1})", callback, content);
                contentType = "application/javascript";
            }

            context.Response.StatusCode = (int)statusCode;
            context.Response.Headers.Add("Pragma", new string[] { "no-cache" });
            context.Response.Headers.Add("Cache-Control", new string[] { "no-cache" });
            context.Response.Headers.Add("Expires", new string[] { "0" });
            context.Response.ContentType = contentType;

            return context.Response.WriteAsync(responseString);
        }

        public static void WriteResponse(IOwinContext context, ClientException e)
        {
            WriteResponse(context, e.StatusCode, e.Content).Wait();
        }


        public static void WriteResponse(IOwinContext context, Exception e, FrameworkLogger logger)
        {
            ServiceHelpers.TraceException(e, logger);
            WriteResponse(context, HttpStatusCode.InternalServerError, "{\"error\":\"Internal server error\"}").Wait();
        }

        public static void TraceException(Exception e, FrameworkLogger logger)
        {
            if (e is AggregateException)
            {
                foreach (Exception ex in ((AggregateException)e).InnerExceptions)
                {
                    TraceException(ex, logger);
                }
            }
            else
            {
                logger.LogError($"{e.GetType().Name} {e.Message}", e);

                if (e.InnerException != null)
                {
                    TraceException(e.InnerException, logger);
                }
            }
        }

        public static bool IsAuthorized()
        {
            Claim scopeClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope");
            bool authorized = (scopeClaim != null && scopeClaim.Value == "user_impersonation");
            return authorized;
        }

        public static string GetTenant()
        {
            if (IsAuthorized())
            {
                Claim tenantIdClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid");
                if (tenantIdClaim != null)
                {
                    return tenantIdClaim.Value;
                }
            }
            return null;
        }

        public static string GetNameIdentifier()
        {
            Claim nameIdentifierClaim = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier);
            if (nameIdentifierClaim != null)
            {
                return nameIdentifierClaim.Value;
            }
            return string.Empty;
        }

        public static void AddField(JObject obj, Document document, string to, string from)
        {
            string value = document.Get(from);
            if (value != null)
            {
                obj[to] = value;
            }
        }

        public static void AddFieldBool(JObject obj, Document document, string to, string from)
        {
            string value = document.Get(from);
            if (value != null)
            {
                obj[to] = value.Equals("True", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public static void AddFieldAsObject(JObject obj, Document document, string to, string from)
        {
            string value = document.Get(from);
            if (value != null)
            {
                obj[to] = JObject.Parse(value);
            }
        }

        public static void AddFieldAsArray(JObject obj, Document document, string to, string from)
        {
            string value = document.Get(from);
            if (value != null)
            {
                obj[to] = new JArray(value.Split(' '));
            }
        }
    }
}
