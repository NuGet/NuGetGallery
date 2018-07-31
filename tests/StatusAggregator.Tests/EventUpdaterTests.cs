using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests
{
    public class EventUpdaterTests
    {
        private const string RowKey = "rowkey";
        private const int EventEndDelayMinutes = 5;
        private static readonly TimeSpan EventEndDelay = TimeSpan.FromMinutes(EventEndDelayMinutes);
        private static readonly DateTime NextCreationTime = new DateTime(2018, 7, 10);

        private static IEnumerable<IncidentEntity> ClosableIncidents => new[]
        {
            CreateIncidentEntity(new DateTime(2018, 7, 9)), // Recently closed incident
            CreateIncidentEntity(DateTime.MinValue), // Old incident
        };

        private static IEnumerable<IncidentEntity> UnclosableIncidents => new[]
        {
            CreateIncidentEntity(NextCreationTime + EventEndDelay), // Incident closed too recently
            CreateIncidentEntity() // Active incident
        };

        private Mock<ITableWrapper> _tableWrapperMock { get; }
        private Mock<IMessageUpdater> _messageUpdaterMock { get; }
        private EventUpdater _eventUpdater { get; }
        private EventEntity _eventEntity { get; }

        public EventUpdaterTests()
        {
            var configuration = new StatusAggregatorConfiguration()
            {
                EventEndDelayMinutes = EventEndDelayMinutes
            };

            _tableWrapperMock = new Mock<ITableWrapper>();
            _messageUpdaterMock = new Mock<IMessageUpdater>();
            _eventUpdater = new EventUpdater(
                _tableWrapperMock.Object, 
                _messageUpdaterMock.Object,
                configuration, 
                Mock.Of<ILogger<EventUpdater>>());

            _eventEntity = new EventEntity()
            {
                RowKey = RowKey,
                StartTime = DateTime.MinValue,
                EndTime = null
            };
        }

        [Fact]
        public async Task ThrowsIfEventNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _eventUpdater.UpdateEvent(null, DateTime.MinValue));
        }

        [Fact]
        public async Task ReturnsFalseIfNotActive()
        {
            _eventEntity.EndTime = DateTime.MinValue;

            var result = await _eventUpdater.UpdateEvent(_eventEntity, NextCreationTime);

            Assert.False(result);
        }

        [Fact]
        public async Task ReturnsFalseIfNoLinkedIncidents()
        {
            _tableWrapperMock
                .Setup(x => x.CreateQuery<IncidentEntity>())
                .Returns(new IncidentEntity[0].AsQueryable());

            var result = await _eventUpdater.UpdateEvent(_eventEntity, NextCreationTime);

            Assert.False(result);
        }

        public static IEnumerable<object[]> DoesNotCloseEventIfUnclosableIncident_Data
        {
            get
            {
                foreach (var unclosableIncident in UnclosableIncidents)
                {
                    yield return new object[] { unclosableIncident };
                }
            }
        }

        [Theory]
        [MemberData(nameof(DoesNotCloseEventIfUnclosableIncident_Data))]
        public async Task DoesNotCloseEventIfUnclosableIncident(IncidentEntity unclosableIncident)
        {
            _tableWrapperMock
                .Setup(x => x.CreateQuery<IncidentEntity>())
                .Returns(ClosableIncidents.Concat(new[] { unclosableIncident }).AsQueryable());

            var result = await _eventUpdater.UpdateEvent(_eventEntity, NextCreationTime);

            Assert.False(result);
            Assert.Null(_eventEntity.EndTime);
            _messageUpdaterMock.Verify(
                x => x.CreateMessageForEventStart(_eventEntity, NextCreationTime),
                Times.Once());
            _messageUpdaterMock.Verify(
                x => x.CreateMessageForEventEnd(It.IsAny<EventEntity>()),
                Times.Never());
        }

        [Fact]
        public async Task ClosesEventIfClosableIncidents()
        {
            _tableWrapperMock
                .Setup(x => x.CreateQuery<IncidentEntity>())
                .Returns(ClosableIncidents.AsQueryable());

            var result = await _eventUpdater.UpdateEvent(_eventEntity, NextCreationTime);

            var expectedEndTime = ClosableIncidents.Max(i => i.MitigationTime ?? DateTime.MinValue);
            Assert.True(result);
            Assert.Equal(expectedEndTime, _eventEntity.EndTime);
            _tableWrapperMock.Verify(
                x => x.InsertOrReplaceAsync(_eventEntity),
                Times.Once());
            _messageUpdaterMock.Verify(
                x => x.CreateMessageForEventStart(_eventEntity, expectedEndTime),
                Times.Once());
            _messageUpdaterMock.Verify(
                x => x.CreateMessageForEventEnd(_eventEntity),
                Times.Once());
        }

        private static IncidentEntity CreateIncidentEntity(DateTime? mitigationTime = null)
        {
            return new IncidentEntity()
            {
                PartitionKey = IncidentEntity.DefaultPartitionKey,
                EventRowKey = RowKey,
                CreationTime = DateTime.MinValue,
                MitigationTime = mitigationTime
            };
        }
    }
}
