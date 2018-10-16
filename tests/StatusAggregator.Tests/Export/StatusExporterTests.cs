// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using StatusAggregator.Export;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class StatusExporterTests
    {
        public class TheExportMethod : StatusExporterTest
        {
            [Fact]
            public async Task ExportsAtCursor()
            {
                var cursor = new DateTime(2018, 9, 13);

                var component = new TestComponent("hi");
                ComponentExporter
                    .Setup(x => x.Export())
                    .Returns(component);

                var events = new Event[0];
                EventExporter
                    .Setup(x => x.Export(cursor))
                    .Returns(events);
                
                await Exporter.Export(cursor);

                Serializer
                    .Verify(
                        x => x.Serialize(cursor, component, events),
                        Times.Once());
            }
        }

        public class StatusExporterTest
        {
            public Mock<IComponentExporter> ComponentExporter { get; }
            public Mock<IEventsExporter> EventExporter { get; }
            public Mock<IStatusSerializer> Serializer { get; }
            public StatusExporter Exporter { get; }

            public StatusExporterTest()
            {
                ComponentExporter = new Mock<IComponentExporter>();
                EventExporter = new Mock<IEventsExporter>();
                Serializer = new Mock<IStatusSerializer>();

                Exporter = new StatusExporter(
                    ComponentExporter.Object,
                    EventExporter.Object,
                    Serializer.Object,
                    Mock.Of<ILogger<StatusExporter>>());
            }
        }
    }
}