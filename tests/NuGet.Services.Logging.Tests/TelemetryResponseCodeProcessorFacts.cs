// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class TelemetryResponseCodeProcessorFacts
    {
        [Theory]
        [InlineData(new int[] { 401 }, "401")]
        [InlineData(new int[] { 400, 404 }, "400")]
        [InlineData(new int[] { 400, 404 }, "404")]
        public void MarksOverridenResponseCodesAsSuccessful(int[] responseCodeOverrides, string responseCode)
        {
            // Arrange
            var failed = new RequestTelemetry
            {
                ResponseCode = responseCode,
                Success = false,
            };

            var successful = new RequestTelemetry
            {
                ResponseCode = responseCode,
                Success = true,
            };

            // Assert
            Assert.True(ProcessResponseCode(responseCodeOverrides, failed).Success);
            Assert.True(ProcessResponseCode(responseCodeOverrides, successful).Success);
        }

        [Theory]
        [InlineData(new int[] { 200 }, "400")]
        [InlineData(new int[] { 400, 404 }, "200")]
        [InlineData(new int[] { 400, 404 }, "301")]
        [InlineData(new int[] { 400, 404 }, "401")]
        [InlineData(new int[] { 400, 404 }, "403")]
        [InlineData(new int[] { 400, 404 }, "410")]
        [InlineData(new int[] { 400, 404 }, "500")]
        public void DoesntAffectOtherResponses(int[] responseCodeOverrides, string responseCode)
        {
            // Arrange
            var failed = new RequestTelemetry
            {
                ResponseCode = responseCode,
                Success = false,
            };

            var successful = new RequestTelemetry
            {
                ResponseCode = responseCode,
                Success = true,
            };

            // Assert
            Assert.False(ProcessResponseCode(responseCodeOverrides, failed).Success);
            Assert.True(ProcessResponseCode(responseCodeOverrides, successful).Success);
        }

        private class TelemetryCallbackProcessor : ITelemetryProcessor
        {
            private Action<ITelemetry> _callback;

            public TelemetryCallbackProcessor(Action<ITelemetry> callback)
            {
                _callback = callback;
            }

            public void Process(ITelemetry item)
            {
                _callback(item);
            }
        }

        private RequestTelemetry ProcessResponseCode(int[] successCodeOverrides, RequestTelemetry telemetry)
        {
            ITelemetry result = null;
            var processor = new TelemetryResponseCodeProcessor(new TelemetryCallbackProcessor(i => result = i));

            foreach (var successCode in successCodeOverrides)
            {
                processor.SuccessfulResponseCodes.Add(successCode);
            }

            processor.Process(telemetry);

            return result as RequestTelemetry;
        }
    }
}
