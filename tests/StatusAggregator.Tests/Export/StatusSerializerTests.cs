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
                var cursor = new DateTime(2018, 9, 13);
                var component = new TestComponent("hi", new[] { new TestComponent("yo"), new TestComponent("what's up") });
                var events = new[] { new Event("", cursor, cursor, new[] { new Message(cursor, "howdy") }) };

                var expectedStatus = new ServiceStatus(cursor, component, events);
                var expectedJson = JsonConvert.SerializeObject(expectedStatus, StatusSerializer.Settings);

                await Serializer.Serialize(cursor, component, events);

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