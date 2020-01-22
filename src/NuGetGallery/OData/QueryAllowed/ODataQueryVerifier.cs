// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Http.OData.Query;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.OData.QueryFilter
{
    public sealed class ODataQueryVerifier
    {
        private static Lazy<ODataQueryFilter> _v2GetUpdates =
            new Lazy<ODataQueryFilter>(() => { return new ODataQueryFilter("apiv2getupdates.json"); }, isThreadSafe: true);
        private static Lazy<ODataQueryFilter> _v2Packages =
            new Lazy<ODataQueryFilter>(() => { return new ODataQueryFilter("apiv2packages.json"); }, isThreadSafe: true);
        private static Lazy<ODataQueryFilter> _v2Search =
            new Lazy<ODataQueryFilter>(() => { return new ODataQueryFilter("apiv2search.json"); }, isThreadSafe: true);
        private static Lazy<ODataQueryFilter> _v1Packages =
            new Lazy<ODataQueryFilter>(() => { return new ODataQueryFilter("apiv1packages.json"); }, isThreadSafe: true);
        private static Lazy<ODataQueryFilter> _v1Search =
            new Lazy<ODataQueryFilter>(() => { return new ODataQueryFilter("apiv1search.json"); }, isThreadSafe: true);

        private readonly ITelemetryService _telemetryService;

        private ODataQueryVerifier(ITelemetryService telemetryService)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public static ODataQueryVerifier Build(ITelemetryService telemetryService)
        {
            return new ODataQueryVerifier(telemetryService);
        }

        #region Filters for ODataV2FeedController
        /// <summary>
        /// The OData query filter for /api/v2/GetUpdates().
        /// </summary>
        public ODataQueryFilter V2GetUpdates
        {
            get
            {
                return _v2GetUpdates.Value;
            }
            set
            {
                _v2GetUpdates = new Lazy<ODataQueryFilter>(() => { return value; });
            }
        }

        /// <summary>
        /// The OData query filter for /api/v2/Packages.
        /// </summary>
        public ODataQueryFilter V2Packages
        {
            get
            {
                return _v2Packages.Value;
            }
            set
            {
                _v2Packages = new Lazy<ODataQueryFilter>(() => { return value; });
            }
        }

        /// <summary>
        /// The OData query filter for /api/v2/Search().
        /// </summary>
        public ODataQueryFilter V2Search
        {
            get
            {
                return _v2Search.Value;
            }
            set
            {
                _v2Search = new Lazy<ODataQueryFilter>(() => { return value; });
            }
        }
        #endregion Filters for ODataV2FeedController

        #region Filters for ODataV1FeedController
        /// <summary>
        /// The OData query filter for /api/v1/Packages.
        /// </summary>
        public ODataQueryFilter V1Packages
        {
            get
            {
                return _v1Packages.Value;
            }
            set
            {
                _v1Packages = new Lazy<ODataQueryFilter>(() => { return value; });
            }
        }

        /// <summary>
        /// The OData query filter for /api/v1/Search()
        /// </summary>
        public ODataQueryFilter V1Search
        {
            get
            {
                return _v1Search.Value;
            }
            set
            {
                _v1Search = new Lazy<ODataQueryFilter>(() => { return value; });
            }
        }
        #endregion Filters for ODataV1FeedController

        /// <summary>
        /// Verifies whether or not the <see cref="ODataQueryOptions(TEntity)"/> are allowed or not.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="odataOptions">The odata options from the request.</param>
        /// <param name="allowedQueryStructure">Each web api will have their individual allowed set of query strutures.</param>
        /// <param name="isFeatureEnabled">The configuration state for the feature.</param>
        /// <param name="telemetryContext">Information to be used by the telemetry events.</param>
        /// <returns>True if the options are allowed.</returns>
        public bool AreODataOptionsAllowed<TEntity>(ODataQueryOptions<TEntity> odataOptions,
                                                           ODataQueryFilter allowedQueryStructure,
                                                           bool isFeatureEnabled,
                                                           string telemetryContext)
        {
            // If validation of the ODataQueryOptions fails, we will not reject the request.
            var isAllowed = true;

            try
            {
                isAllowed = allowedQueryStructure.IsAllowed(odataOptions);
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, properties =>
                {
                    properties.Add(TelemetryService.CallContext, $"{telemetryContext}:{nameof(AreODataOptionsAllowed)}");
                    properties.Add(TelemetryService.IsEnabled, $"{isFeatureEnabled}");
                });
            }

            _telemetryService.TrackODataQueryFilterEvent(
                callContext: $"{telemetryContext}:{nameof(AreODataOptionsAllowed)}",
                isEnabled: isFeatureEnabled,
                isAllowed: isAllowed,
                queryPattern: ODataQueryFilter.ODataOptionsMap(odataOptions).ToString());

            return isFeatureEnabled ? isAllowed : true;
        }

        internal static string GetValidationFailedMessage<T>(ODataQueryOptions<T> options)
        {
            return $"A query with \"{ODataQueryFilter.ODataOptionsMap(options)}\" set of operators is not supported. Please refer to : https://github.com/NuGet/Home/wiki/Filter-OData-query-requests for additional information.";
        }
    }
}