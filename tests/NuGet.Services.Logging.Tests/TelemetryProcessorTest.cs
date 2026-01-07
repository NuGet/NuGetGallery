// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Moq;

namespace NuGet.Services.Logging.Tests
{
    internal sealed class TelemetryProcessorTest<T> : IDisposable
        where T : ITelemetryProcessor
    {
        private readonly TelemetryClient _client;
        private readonly TelemetryConfiguration _configuration;
        private bool _isDisposed;

        internal Mock<ITelemetryProcessor> NextProcessor { get; }
        internal T Processor { get; }
        internal List<ITelemetry> SentTelemetry { get; }

        internal TelemetryProcessorTest(
            T processor,
            Mock<ITelemetryProcessor> nextProcessor,
            TelemetryClient client,
            TelemetryConfiguration configuration,
            List<ITelemetry> sentTelemetry)
        {
            Processor = processor;
            NextProcessor = nextProcessor;
            _client = client;
            _configuration = configuration;
            SentTelemetry = sentTelemetry;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _configuration.Dispose();

                _isDisposed = true;
                GC.SuppressFinalize(this);

                NextProcessor.Verify();
            }
        }

        internal static TelemetryProcessorTest<T> Create()
        {
            var sentTelemetry = new List<ITelemetry>();
            var configuration = new TelemetryConfiguration()
            {
                DisableTelemetry = false,
                InstrumentationKey = Guid.Empty.ToString(),
                TelemetryChannel = new StubTelemetryChannel()
                {
                    OnSend = telemetry => sentTelemetry.Add(telemetry)
                }
            };
            var client = new TelemetryClient(configuration);
            var nextProcessor = new Mock<ITelemetryProcessor>(MockBehavior.Strict);
            T processor;

            if (typeof(T) == typeof(RequestTelemetryProcessor))
            {
                processor = (T)(object)new RequestTelemetryProcessor(nextProcessor.Object);
            }
            else if (typeof(T) == typeof(ExceptionTelemetryProcessor))
            {
                processor = (T)(object)new ExceptionTelemetryProcessor(nextProcessor.Object, client);
            }
            else
            {
                throw new NotImplementedException();
            }

            return new TelemetryProcessorTest<T>(
                processor,
                nextProcessor,
                client,
                configuration,
                sentTelemetry);
        }

        private sealed class StubTelemetryChannel : ITelemetryChannel
        {
            public bool? DeveloperMode { get; set; }
            public string EndpointAddress { get; set; }
            public Action<ITelemetry> OnSend { get; set; }

            public void Dispose()
            {
            }

            public void Flush()
            {
            }

            public void Send(ITelemetry item)
            {
                OnSend(item);
            }
        }
    }
}