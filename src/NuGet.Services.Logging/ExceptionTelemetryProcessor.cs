// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Logging
{
    /// <summary>
    /// An Application Insights telemetry processor for <see cref="ExceptionTelemetry" />.
    /// </summary>
    public sealed class ExceptionTelemetryProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _nextProcessor;
        private readonly TelemetryClient _telemetryClient;

        /// <summary>
        /// Initialize a new <see cref="ExceptionTelemetryProcessor" /> class.
        /// </summary>
        /// <param name="nextProcessor">The next telemetry processor in the processing chain.</param>
        /// <param name="telemetryClient">A telemetry client.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="nextProcessor" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="telemetryClient" />
        /// is <c>null</c>.</exception>
        public ExceptionTelemetryProcessor(ITelemetryProcessor nextProcessor, TelemetryClient telemetryClient)
        {
            _nextProcessor = nextProcessor ?? throw new ArgumentNullException(nameof(nextProcessor));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        /// <summary>
        /// Processes a telemetry item.
        /// </summary>
        /// <param name="item">A telemetry item.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="item" /> is <c>null</c>.</exception>
        public void Process(ITelemetry item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var exceptionTelemetry = item as ExceptionTelemetry;

            if (exceptionTelemetry != null)
            {
                var httpException = exceptionTelemetry.Exception as HttpException;

                if (httpException != null)
                {
                    var httpCode = httpException.GetHttpCode();

                    if (httpCode < 500)
                    {
                        // Logging exception telemetry for a request that resulted in an HTTP response code under 500
                        // adds noise to exception telemetry analysis.  Log it as trace telemetry instead.
                        var traceTelemetry = Convert(exceptionTelemetry, httpException);

                        _telemetryClient.TrackTrace(traceTelemetry);

                        return;
                    }
                }
            }

            _nextProcessor.Process(item);
        }

        private static TraceTelemetry Convert(ExceptionTelemetry exceptionTelemetry, HttpException exception)
        {
            var traceTelemetry = new TraceTelemetry(exception.Message, SeverityLevel.Warning);

            traceTelemetry.Context.Cloud.RoleInstance = exceptionTelemetry.Context.Cloud.RoleInstance;
            traceTelemetry.Context.Cloud.RoleName = exceptionTelemetry.Context.Cloud.RoleName;
            traceTelemetry.Context.GetInternalContext().SdkVersion = exceptionTelemetry.Context.GetInternalContext().SdkVersion;
            traceTelemetry.Context.InstrumentationKey = exceptionTelemetry.Context.InstrumentationKey;
            traceTelemetry.Context.Operation.Id = exceptionTelemetry.Context.Operation.Id;
            traceTelemetry.Context.Operation.Name = exceptionTelemetry.Context.Operation.Name;
            traceTelemetry.Context.Operation.ParentId = exceptionTelemetry.Context.Operation.ParentId;
            traceTelemetry.Context.Location.Ip = exceptionTelemetry.Context.Location.Ip;

            foreach (var property in exceptionTelemetry.Properties)
            {
                traceTelemetry.Properties.Add(property.Key, property.Value);
            }

            traceTelemetry.Properties.Add("exception", JObject.FromObject(exception).ToString(Formatting.None));

            traceTelemetry.Sequence = exceptionTelemetry.Sequence;
            traceTelemetry.Timestamp = exceptionTelemetry.Timestamp;

            return traceTelemetry;
        }
    }
}