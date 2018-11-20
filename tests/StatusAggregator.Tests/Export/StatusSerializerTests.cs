// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Status;
using StatusAggregator.Container;
using StatusAggregator.Export;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class StatusSerializerTests
    {
        public class TheSerializeMethod : StatusSerializerTest
        {
            [Fact]
            public async Task SerializesStatus()
            {
                var lastBuilt = new DateTime(2018, 11, 13);
                var lastUpdated = new DateTime(2018, 9, 13);
                var component = new TestComponent("hi", new[] { new TestComponent("yo"), new TestComponent("what's up") });
                var events = new[] { new Event("", lastUpdated, lastUpdated, new[] { new Message(lastUpdated, "howdy") }) };

                var expectedStatus = new ServiceStatus(lastBuilt, lastUpdated, component, events);
                var expectedJson = JsonConvert.SerializeObject(expectedStatus, StatusSerializer.Settings);

                await Serializer.Serialize(lastBuilt, lastUpdated, component, events);

                Container
                    .Verify(
                        x => x.SaveBlobAsync(StatusSerializer.StatusBlobName, expectedJson),
                        Times.Once());
            }
        }

        public class StatusSerializerTest
        {
            public Mock<IContainerWrapper> Container { get; }
            public StatusSerializer Serializer { get; }

            public StatusSerializerTest()
            {
                Container = new Mock<IContainerWrapper>();

                Serializer = new StatusSerializer(
                    Container.Object,
                    Mock.Of<ILogger<StatusSerializer>>());
            }
        }
    }
}