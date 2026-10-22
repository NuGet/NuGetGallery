// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.Logging;
using NuGet.Services.Staging;
using Xunit;

namespace NuGet.Services.Staging.Promotion.Tests
{
    /// <summary>
    /// Verifies message metric units and low-cardinality dimensions.
    /// </summary>
    public class StagingPromotionTelemetryServiceFacts
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RecordsMessageMetricsInSecondsWithoutPerDeliveryDimensions(bool handled)
        {
            var client = new Mock<ITelemetryClient>(MockBehavior.Strict);
            client.Setup(telemetry => telemetry.TrackMetric("StagingPromotion.MessageDeliveryLagSeconds", 2.5,
                It.Is<IDictionary<string, string>>(properties => properties.Count == 1 && properties["MessageType"] == nameof(StagingPromotionMessage))));
            client.Setup(telemetry => telemetry.TrackMetric("StagingPromotion.MessageEnqueueLagSeconds", 0.25,
                It.Is<IDictionary<string, string>>(properties => properties.Count == 1 && properties["MessageType"] == nameof(StagingPromotionMessage))));
            client.Setup(telemetry => telemetry.TrackMetric("StagingPromotion.MessageHandlerDurationSeconds", 3.5,
                It.Is<IDictionary<string, string>>(properties => properties.Count == 2 && properties["MessageType"] == nameof(StagingPromotionMessage)
                    && properties["Handled"] == handled.ToString())));
            client.Setup(telemetry => telemetry.TrackMetric("StagingPromotion.MessageLockLost", 1,
                It.Is<IDictionary<string, string>>(properties => properties.Count == 1 && properties["MessageType"] == nameof(StagingPromotionMessage))));
            var target = new StagingPromotionTelemetryService(client.Object);
            var callGuid = Guid.NewGuid();

            target.TrackMessageDeliveryLag<StagingPromotionMessage>(TimeSpan.FromMilliseconds(2500));
            target.TrackEnqueueLag<StagingPromotionMessage>(TimeSpan.FromMilliseconds(250));
            target.TrackMessageHandlerDuration<StagingPromotionMessage>(TimeSpan.FromMilliseconds(3500), callGuid, handled);
            target.TrackMessageLockLost<StagingPromotionMessage>(callGuid);

            client.VerifyAll();
            Assert.Equal(4, client.Invocations.Count);
        }
    }
}
