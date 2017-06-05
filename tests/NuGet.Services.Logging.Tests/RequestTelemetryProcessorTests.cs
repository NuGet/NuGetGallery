// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class RequestTelemetryProcessorTests
    {
        [Fact]
        public void Constructor_ThrowsForNullNext()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new RequestTelemetryProcessor(nextProcessor: null));

            Assert.Equal("nextProcessor", exception.ParamName);
        }

        [Fact]
        public void SuccessfulResponseCodes_IsEmptyByDefault()
        {
            using (var test = TelemetryProcessorTest<RequestTelemetryProcessor>.Create())
            {
                Assert.Empty(test.Processor.SuccessfulResponseCodes);
            }
        }

        [Fact]
        public void Process_ThrowsForNullItem()
        {
            using (var test = TelemetryProcessorTest<RequestTelemetryProcessor>.Create())
            {
                var exception = Assert.Throws<ArgumentNullException>(() => test.Processor.Process(item: null));

                Assert.Equal("item", exception.ParamName);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Process_SetsSuccessToTrueForRequestTelemetryWithSuccessfulResponseCode(bool actualSuccess)
        {
            var responseCode = 400;
            var telemetry = new RequestTelemetry()
            {
                ResponseCode = responseCode.ToString(),
                Success = actualSuccess
            };

            using (var test = TelemetryProcessorTest<RequestTelemetryProcessor>.Create())
            {
                test.NextProcessor.Setup(x => x.Process(It.IsNotNull<RequestTelemetry>()))
                    .Verifiable();

                test.Processor.SuccessfulResponseCodes.Add(responseCode);
                test.Processor.Process(telemetry);

                Assert.True(telemetry.Success);
            }
        }

        [Theory]
        [InlineData(400)]
        [InlineData(404)]
        public void Process_MultipleSuccessfulResponseCodes_SetsSuccessToTrueForRequestTelemetryWithSuccessfulResponseCode(
            int responseCode)
        {
            var telemetry = new RequestTelemetry()
            {
                ResponseCode = responseCode.ToString(),
                Success = false
            };

            using (var test = TelemetryProcessorTest<RequestTelemetryProcessor>.Create())
            {
                test.NextProcessor.Setup(x => x.Process(It.IsNotNull<RequestTelemetry>()))
                    .Verifiable();

                test.Processor.SuccessfulResponseCodes.Add(400);
                test.Processor.SuccessfulResponseCodes.Add(404);

                test.Processor.Process(telemetry);

                Assert.True(telemetry.Success);
            }
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(null, false)]
        [InlineData("", true)]
        [InlineData("", false)]
        [InlineData("a", true)]
        [InlineData("a", false)]
        [InlineData("-1", true)]
        [InlineData("-1", false)]
        [InlineData("1.2", true)]
        [InlineData("1.2", false)]
        [InlineData("401", true)]
        [InlineData("401", false)]
        public void Process_DoesNotModifySuccessForRequestTelemetryWithOtherResponseCodes(
            string responseCode,
            bool actualSuccess)
        {
            var telemetry = new RequestTelemetry()
            {
                ResponseCode = responseCode,
                Success = actualSuccess
            };

            using (var test = TelemetryProcessorTest<RequestTelemetryProcessor>.Create())
            {
                test.NextProcessor.Setup(x => x.Process(It.IsNotNull<RequestTelemetry>()))
                    .Verifiable();

                test.Processor.SuccessfulResponseCodes.Add(400);
                test.Processor.Process(telemetry);

                Assert.Equal(actualSuccess, telemetry.Success);
            }
        }

        [Theory]
        [InlineData(400)]
        [InlineData(401)]
        public void Process_AlwaysCallsNextProcessorForRequestTelemetry(int responseCode)
        {
            var telemetry = new RequestTelemetry()
            {
                ResponseCode = responseCode.ToString(),
                Success = false
            };

            using (var test = TelemetryProcessorTest<RequestTelemetryProcessor>.Create())
            {
                test.NextProcessor.Setup(x => x.Process(It.Is<RequestTelemetry>(rt => ReferenceEquals(rt, telemetry))));

                test.Processor.SuccessfulResponseCodes.Add(400);
                test.Processor.Process(telemetry);

                test.NextProcessor.Verify(
                    x => x.Process(It.Is<RequestTelemetry>(rt => ReferenceEquals(rt, telemetry))),
                    Times.Once());
            }
        }
    }
}