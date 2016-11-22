// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Web.Http.OData.Query;

namespace NuGetGallery.OData.QueryWhitelist
{
   
    internal class ODataQueryVerifier
    {
        /// <summary>
        /// Logic for validation if the odataOptions will be rejected or not.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="odataOptions">The odata options from the request.</param>
        /// <param name="allowedQueryStructure">Each web api will have their individual whitelist of query strutures.</param>
        /// <param name="telemetryContext">Information to be used by the telemetry events.</param>
        /// <returns></returns>
        internal static bool AreODataOptionsAllowed<TEntity>(ODataQueryOptions<TEntity> odataOptions, IODataQueryOptionsWhitelist allowedQueryStructure, string telemetryContext)
        {
            Dictionary<string, string> telemetryProperties = new Dictionary<string, string>();
            telemetryProperties.Add("MethodName", $"{telemetryContext}:{nameof(AreODataOptionsAllowed)}");
            string currentOptions = string.Empty;
            //default is true; in case of exception the return value will be true and the request will not be rejected
            bool result = true;

            try
            {
                currentOptions = ReadOptions(odataOptions);
                result = allowedQueryStructure.IsWhitelisted(currentOptions);
                telemetryProperties.Add("Exception", string.Empty);
            }
            catch(Exception ex)
            {
                telemetryProperties.Add("Exception", ex.Message);
                Telemetry.TrackException(ex, telemetryProperties);
                //log and do not throw
            }

            telemetryProperties.Add("IsWhitelisted", result.ToString());
            telemetryProperties.Add("QueryPattern", currentOptions);
            Telemetry.TrackEvent(Telemetry._events["QueryWhitelist"], telemetryProperties, null);
            return result;

        }

        static string ReadOptions<TEntity>(ODataQueryOptions<TEntity> odataOptions)
        {
            List<string> values = new List<string>();
            
            if (odataOptions.Filter != null) values.Add(nameof(odataOptions.Filter).ToLower());
            if (odataOptions.IfMatch != null) values.Add(nameof(odataOptions.IfMatch).ToLower());
            if (odataOptions.IfNoneMatch != null) values.Add(nameof(odataOptions.IfNoneMatch).ToLower());
            if (odataOptions.InlineCount != null) values.Add(nameof(odataOptions.InlineCount).ToLower());
            if (odataOptions.OrderBy != null) values.Add(nameof(odataOptions.OrderBy).ToLower());
            if (odataOptions.SelectExpand != null) values.Add(nameof(odataOptions.SelectExpand).ToLower());
            if (odataOptions.Skip != null) values.Add(nameof(odataOptions.Skip).ToLower());
            if (odataOptions.Top != null) values.Add(nameof(odataOptions.Top).ToLower());
            if (odataOptions.RawValues.SkipToken != null) values.Add(nameof(odataOptions.RawValues.SkipToken).ToLower());
            StringBuilder sb = new StringBuilder();
            foreach (string arg in values.OrderBy(v => v))
            {
                sb.Append(arg);
                sb.Append(",");
            }
            if (sb.Length > 0) { sb.Remove(sb.Length - 1, 1); }
            return sb.ToString();
        }
    }
}