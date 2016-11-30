// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Http.OData.Query;

namespace NuGetGallery.OData.QueryFilter
{
    public class ODataQueryVerifier
    {
        private static readonly string _v2FindPackagesByIdResource = "apiv2FindPackagesById.json";
        private static Lazy<ODataQueryFilter> _v2FindPackagesById = new Lazy<ODataQueryFilter> (
                                                                        ()=> 
                                                                        {
                                                                            return new ODataQueryFilter(_v2FindPackagesByIdResource);
                                                                        },
                                                                        true);

        /// <summary>
        /// The OData query filter for the api/v2/FindPackagesById
        /// </summary>
        public static ODataQueryFilter V2FindPackagesByIdQueryFilter
        {
            get
            {
                return _v2FindPackagesById.Value;
            }
        }

        /// <summary>
        /// Logic for validation if the odataOptions will be rejected or not.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="odataOptions">The odata options from the request.</param>
        /// <param name="allowedQueryStructure">Each web api will have their individual whitelist of query strutures.</param>
        /// <param name="telemetryContext">Information to be used by the telemetry events.</param>
        /// <returns></returns>
        internal static bool AreODataOptionsAllowed<TEntity>(ODataQueryOptions<TEntity> odataOptions,
                                                             ODataQueryFilter allowedQueryStructure,
                                                             string telemetryContext)
        {
            var telemetryProperties = new Dictionary<string, string>();
            telemetryProperties.Add("CallContext", $"{telemetryContext}:{nameof(AreODataOptionsAllowed)}");
            //default is true; in case of exception the return value will be true and the request will not be rejected
            var result = true;

            try
            {
                result = allowedQueryStructure.IsAllowed(odataOptions);
            }
            catch (Exception ex)
            {
                //log and do not throw
                telemetryProperties.Add("Exception", ex.Message);
                Telemetry.TrackException(ex, telemetryProperties);
            }

            telemetryProperties.Add("IsAllowed", result.ToString());
            telemetryProperties.Add("QueryPattern", ODataQueryFilter.ODataOptionsMap(odataOptions).ToString());
            Telemetry.TrackEvent("ODataQueryFilter", telemetryProperties, metrics: null);
            return result;
        }
    }
}