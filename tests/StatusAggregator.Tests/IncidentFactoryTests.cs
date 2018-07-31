using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests
{
    public class IncidentFactoryTests
    {
        private const string Id = "id";
        private const string AffectedComponentPath = "path";
        private const ComponentStatus AffectedComponentStatus = ComponentStatus.Degraded;
        private static DateTime CreationTime = new DateTime(2017, 7, 10);

        private Mock<ITableWrapper> _tableWrapperMock { get; }
        private Mock<IEventUpdater> _eventUpdaterMock { get; }
        private IncidentFactory _incidentFactory { get; }
        private ParsedIncident _parsedIncident { get; }

        public IncidentFactoryTests()
        {
            _tableWrapperMock = new Mock<ITableWrapper>();
            _eventUpdaterMock = new Mock<IEventUpdater>();
            _incidentFactory = new IncidentFactory(
                _tableWrapperMock.Object, 
                _eventUpdaterMock.Object,
                Mock.Of<ILogger<IncidentFactory>>());

            var incident = new Incident() { Id = Id, Source = new IncidentSourceData() { CreateDate = CreationTime } };
            _parsedIncident = new ParsedIncident(incident, AffectedComponentPath, AffectedComponentStatus);
        }

        [Fact]
        public async Task CreatesNewEventIfNoPossibleEvents()
        {
            _tableWrapperMock
                .Setup(x => x.CreateQuery<EventEntity>())
                .Returns(new EventEntity[0].AsQueryable());

            _tableWrapperMock
                .Setup(x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()))
                .Returns(Task.CompletedTask);

            EventEntity eventEntity = null;
            _tableWrapperMock
                .Setup(x => x.InsertOrReplaceAsync(It.Is<ITableEntity>(e => e is EventEntity)))
                .Callback<ITableEntity>(e => { eventEntity = e as EventEntity; })
                .Returns(Task.CompletedTask);

            var incidentEntity = await _incidentFactory.CreateIncident(_parsedIncident);

            Assert.Equal(Id, incidentEntity.IncidentApiId);
            Assert.Equal(CreationTime, incidentEntity.CreationTime);
            Assert.Equal(AffectedComponentPath, incidentEntity.AffectedComponentPath);
            Assert.Equal((int)AffectedComponentStatus, incidentEntity.AffectedComponentStatus);
            Assert.NotNull(eventEntity);
            Assert.Equal(eventEntity.RowKey, incidentEntity.EventRowKey);
            Assert.Equal(IncidentEntity.DefaultPartitionKey, incidentEntity.PartitionKey);
            Assert.True(incidentEntity.IsLinkedToEvent);
            Assert.True(incidentEntity.IsActive);

            _tableWrapperMock.Verify(
                x => x.InsertOrReplaceAsync(incidentEntity),
                Times.Once());

            _tableWrapperMock.Verify(
                x => x.InsertOrReplaceAsync(eventEntity),
                Times.Once());

            _tableWrapperMock.Verify(
                x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()),
                Times.Exactly(2));
        }
    }
}
