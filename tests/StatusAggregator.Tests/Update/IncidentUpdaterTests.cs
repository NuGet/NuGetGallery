// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Update
{
    public class IncidentUpdaterTests
    {
        public class TheUpdateAsyncMethod
            : IncidentUpdaterTest
        {
            [Fact]
            public async Task IgnoresDeactivatedIncidents()
            {
                var cursor = new DateTime(2018, 10, 10);
                var endTime = new DateTime(2018, 10, 11);
                var incidentEntity = new IncidentEntity
                {
                    EndTime = endTime
                };

                await Updater.UpdateAsync(incidentEntity, cursor);

                Assert.Equal(endTime, incidentEntity.EndTime);

                Client
                    .Verify(
                        x => x.GetIncident(It.IsAny<string>()),
                        Times.Never());

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Never());
            }

            [Fact]
            public async Task DoesNotSaveIfNotMitigated()
            {
                var cursor = new DateTime(2018, 10, 10);
                var incidentEntity = new IncidentEntity
                {
                    IncidentApiId = "id"
                };

                var incident = new Incident();
                Client
                    .Setup(x => x.GetIncident(incidentEntity.IncidentApiId))
                    .ReturnsAsync(incident);

                await Updater.UpdateAsync(incidentEntity, cursor);

                Assert.True(incidentEntity.IsActive);

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Never());
            }

            [Fact]
            public async Task DeactivatesIfMitigated()
            {
                var cursor = new DateTime(2018, 10, 10);
                var incidentEntity = new IncidentEntity
                {
                    IncidentApiId = "id"
                };

                var incident = new Incident
                {
                    MitigationData = new IncidentStateChangeEventData
                    {
                        Date = new DateTime(2018, 10, 11)
                    }
                };

                Client
                    .Setup(x => x.GetIncident(incidentEntity.IncidentApiId))
                    .ReturnsAsync(incident);

                Table
                    .Setup(x => x.ReplaceAsync(incidentEntity))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Updater.UpdateAsync(incidentEntity, cursor);

                Assert.Equal(incident.MitigationData.Date, incidentEntity.EndTime);
                Assert.False(incidentEntity.IsActive);

                Table.Verify();
            }

            [Fact]
            public async Task DeactivatesIfClientReturns404()
            {
                var cursor = new DateTime(2018, 10, 10);
                var incidentEntity = new IncidentEntity
                {
                    IncidentApiId = "id"
                };

                var response = new Mock<HttpWebResponse>();
                response.Setup(r => r.StatusCode).Returns(HttpStatusCode.NotFound);

                Client
                    .Setup(x => x.GetIncident(incidentEntity.IncidentApiId))
                    .ThrowsAsync(new WebException(null, null, WebExceptionStatus.ProtocolError, response.Object));

                Table
                    .Setup(x => x.ReplaceAsync(incidentEntity))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Updater.UpdateAsync(incidentEntity, cursor);

                Assert.NotNull(incidentEntity.EndTime);
                Assert.False(incidentEntity.IsActive);

                Table.Verify();
            }

            [Fact]
            public async Task RethrowsIfClientThrowsWebExceptionButNot404()
            {
                var cursor = new DateTime(2018, 10, 10);
                var incidentEntity = new IncidentEntity
                {
                    IncidentApiId = "id"
                };

                var response = new Mock<HttpWebResponse>();
                response.Setup(r => r.StatusCode).Returns(HttpStatusCode.ServiceUnavailable);

                Client
                    .Setup(x => x.GetIncident(incidentEntity.IncidentApiId))
                    .ThrowsAsync(new WebException(null, null, WebExceptionStatus.ProtocolError, response.Object));

                await Assert.ThrowsAsync<WebException>(() => Updater.UpdateAsync(incidentEntity, cursor));

                Assert.Null(incidentEntity.EndTime);
                Assert.True(incidentEntity.IsActive);

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Never());
            }
        }

        public class IncidentUpdaterTest
        {
            public Mock<ITableWrapper> Table { get; }
            public Mock<IIncidentApiClient> Client { get; }

            public IncidentUpdater Updater { get; }

            public IncidentUpdaterTest()
            {
                Table = new Mock<ITableWrapper>();

                Client = new Mock<IIncidentApiClient>();

                Updater = new IncidentUpdater(
                    Table.Object,
                    Client.Object,
                    Mock.Of<ILogger<IncidentUpdater>>());
            }
        }
    }
}
