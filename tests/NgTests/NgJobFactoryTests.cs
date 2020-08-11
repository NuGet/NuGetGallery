// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Ng;
using NuGet.Services.Logging;
using Xunit;

namespace NgTests
{
    public class NgJobFactoryTests
    {
        [Theory]
        [MemberData(nameof(CanActivateJobsData))]
        public void CanActivateJobs(string name, Type type)
        {
            // Arrange
            var telemetryClient = new Mock<ITelemetryClient>();
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory
                .Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(() => new Mock<ILogger>().Object);

            // Act
            var job = NgJobFactory.GetJob(name, loggerFactory.Object, telemetryClient.Object, new Dictionary<string, string>());

            // Assert
            Assert.NotNull(job);
            Assert.IsType(type, job);
        }

        public static IEnumerable<object[]> CanActivateJobsData => NgJobFactory
            .JobMap
            .OrderBy(x => x.Key)
            .Select(x => new object[] { x.Key, x.Value });
    }
}
