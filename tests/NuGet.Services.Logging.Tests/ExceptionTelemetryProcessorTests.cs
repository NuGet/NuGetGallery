// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class ExceptionTelemetryProcessorTests
    {
        [Fact]
        public void Constructor_ThrowsForNullNext()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ExceptionTelemetryProcessor(nextProcessor: null, telemetryClient: new TelemetryClient()));

            Assert.Equal("nextProcessor", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullTelemetryClient()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ExceptionTelemetryProcessor(Mock.Of<ITelemetryProcessor>(), telemetryClient: null));

            Assert.Equal("telemetryClient", exception.ParamName);
        }

        [Fact]
        public void Process_ThrowsForNullItem()
        {
            using (var test = TelemetryProcessorTest<ExceptionTelemetryProcessor>.Create())
            {
                var exception = Assert.Throws<ArgumentNullException>(() => test.Processor.Process(item: null));

                Assert.Equal("item", exception.ParamName);
            }
        }

        [Fact]
        public void Process_CallsNextProcessorForExceptionTelemetryWithNullException()
        {
            var telemetry = new ExceptionTelemetry();

            // By default this property is null.
            // However, assert this fact anyway to guard against an unexpected change in the dependency
            // which would invalidate the setup for this test.
            Assert.Null(telemetry.Exception);

            using (var test = TelemetryProcessorTest<ExceptionTelemetryProcessor>.Create())
            {
                test.NextProcessor.Setup(x => x.Process(It.Is<ExceptionTelemetry>(rt => ReferenceEquals(rt, telemetry))));

                test.Processor.Process(telemetry);

                test.NextProcessor.Verify(
                    x => x.Process(It.Is<ExceptionTelemetry>(rt => ReferenceEquals(rt, telemetry))),
                    Times.Once());
            }
        }

        [Fact]
        public void Process_CallsNextProcessorForExceptionTelemetryWithExceptionThatIsNotHttpException()
        {
            var telemetry = new ExceptionTelemetry(new NullReferenceException());

            using (var test = TelemetryProcessorTest<ExceptionTelemetryProcessor>.Create())
            {
                test.NextProcessor.Setup(x => x.Process(It.Is<ExceptionTelemetry>(rt => ReferenceEquals(rt, telemetry))));

                test.Processor.Process(telemetry);

                test.NextProcessor.Verify(
                    x => x.Process(It.Is<ExceptionTelemetry>(rt => ReferenceEquals(rt, telemetry))),
                    Times.Once());
            }
        }

        [Theory]
        [InlineData(500)]
        [InlineData(503)]
        public void Process_CallsNextProcessorForExceptionTelemetryWithHttpStatusCodeGreaterThanOrEqualTo500(
            int statusCode)
        {
            var telemetry = new ExceptionTelemetry(new HttpException(statusCode, message: "a"));

            using (var test = TelemetryProcessorTest<ExceptionTelemetryProcessor>.Create())
            {
                test.NextProcessor.Setup(x => x.Process(It.Is<ExceptionTelemetry>(rt => ReferenceEquals(rt, telemetry))));

                test.Processor.Process(telemetry);

                test.NextProcessor.Verify(
                    x => x.Process(It.Is<ExceptionTelemetry>(rt => ReferenceEquals(rt, telemetry))),
                    Times.Once());
            }
        }

        [Fact]
        public void Process_TracksNewTraceTelemetryForExceptionTelemetryWithHttpStatusCodeLessThan500()
        {
            var telemetry = new ExceptionTelemetry(new HttpException(httpCode: 499, message: "a"));

            using (var test = TelemetryProcessorTest<ExceptionTelemetryProcessor>.Create())
            {
                test.Processor.Process(telemetry);

                Assert.IsType<TraceTelemetry>(test.SentTelemetry.Single());
            }
        }

        [Fact]
        public void Process_CallsTelemetryClientWithTraceTelemetryPopulatedFromExceptionTelemetry()
        {
            var exception = new HttpException(httpCode: 401, message: "a");
            var exceptionJson = JObject.FromObject(exception).ToString(Formatting.None);
            var timestamp = DateTimeOffset.UtcNow;
            var exceptionTelemetry = new ExceptionTelemetry(exception);

            exceptionTelemetry.Context.Cloud.RoleInstance = "b";
            exceptionTelemetry.Context.Cloud.RoleName = "c";
            exceptionTelemetry.Context.GetInternalContext().SdkVersion = "d";
            exceptionTelemetry.Context.InstrumentationKey = "e";
            exceptionTelemetry.Context.Operation.Id = "f";
            exceptionTelemetry.Context.Operation.Name = "g";
            exceptionTelemetry.Context.Operation.ParentId = "h";
            exceptionTelemetry.Context.Location.Ip = "127.0.0.1";
            exceptionTelemetry.Properties.Add("i", "j");
            exceptionTelemetry.Sequence = "k";
            exceptionTelemetry.Timestamp = timestamp;

            using (var test = TelemetryProcessorTest<ExceptionTelemetryProcessor>.Create())
            {
                test.Processor.Process(exceptionTelemetry);

                var sentTelemetry = test.SentTelemetry.Single() as TraceTelemetry;

                Assert.Equal("a", sentTelemetry.Message);
                Assert.Equal(SeverityLevel.Warning, sentTelemetry.SeverityLevel);
                Assert.Equal("b", sentTelemetry.Context.Cloud.RoleInstance);
                Assert.Equal("c", sentTelemetry.Context.Cloud.RoleName);
                Assert.Equal("d", sentTelemetry.Context.GetInternalContext().SdkVersion);
                Assert.Equal("e", sentTelemetry.Context.InstrumentationKey);
                Assert.Equal("f", sentTelemetry.Context.Operation.Id);
                Assert.Equal("g", sentTelemetry.Context.Operation.Name);
                Assert.Equal("h", sentTelemetry.Context.Operation.ParentId);
                Assert.Equal("127.0.0.1", sentTelemetry.Context.Location.Ip);
                Assert.Equal("j", sentTelemetry.Properties["i"]);
                Assert.Equal("k", sentTelemetry.Sequence);
                Assert.Equal(timestamp, sentTelemetry.Timestamp);
            }
        }
    }
}