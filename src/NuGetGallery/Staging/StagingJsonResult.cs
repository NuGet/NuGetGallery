// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NuGetGallery
{
    public class StagingJsonResult : ActionResult
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatString = "O",
        };

        public StagingJsonResult(HttpStatusCode statusCode, object value)
        {
            StatusCode = statusCode;
            Value = value;
        }

        public HttpStatusCode StatusCode { get; }
        public object Value { get; }

        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.HttpContext.Response.StatusCode = (int)StatusCode;
            context.HttpContext.Response.ContentType = "application/json";
            context.HttpContext.Response.TrySkipIisCustomErrors = StatusCode >= HttpStatusCode.BadRequest;
            context.HttpContext.Response.Write(Serialize(Value));
        }

        internal static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, SerializerSettings);
        }
    }
}
