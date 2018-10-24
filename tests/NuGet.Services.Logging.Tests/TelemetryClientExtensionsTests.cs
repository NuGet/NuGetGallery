// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Moq;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class TelemetryClientExtensionsTests
    {
        public class TrackDuration : Base
        {
            [Fact]
            public void ReturnsDurationMetric()
            {
                Assert.IsType<DurationMetric>(_target.Object.TrackDuration("test"));
            }

            [Fact]
            public void TracksMetricOnDispose()
            {
                // Arrange
                var name = "Hello.World";
                var properties = new Dictionary<string, string>
                {
                    { "foo", "bar" }
                };

                // Act & Assert
                using (_target.Object.TrackDuration(name, properties))
                {
                }

                _target.Verify(t => t.TrackMetric(name, It.Is<double>(d => d > 0), properties));
            }
        }

        public class Base
        {
            protected Mock<ITelemetryClient> _target;

            public Base()
            {
                _target = new Mock<ITelemetryClient>();
            }
        }
    }
}
