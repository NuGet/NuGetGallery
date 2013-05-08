using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery.Statistics
{
    public class AggregateStatsQueryFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public void GivenNoData_ItShouldReturnAnEmptyAggregateStats()
            {
                // Arrange
                var mockContext = new Mock<IEntitiesContext>();
                var mockReader = new Mock<IDataReader>();
                mockReader.Setup(r => r.Read()).Returns(false);
                mockContext.SetupSql<AggregateStats>(
                    AggregateStatsQuery.Sql,
                    mockReader,
                    connectionTimeout: 200, 
                    behavior: CommandBehavior.CloseConnection | CommandBehavior.SingleRow);

                var query = new AggregateStatsQuery() { DatabaseContext = mockContext.Object };

                // Act
                var result = query.Execute();

                // Assert
                Assert.Equal(new AggregateStats(), result);
            }

            [Fact]
            public void GivenNullData_ItShouldReturnAnEmptyAggregateStats()
            {
                // Arrange
                var mockContext = new Mock<IEntitiesContext>();
                var mockReader = new Mock<IDataReader>();
                mockReader.Setup(r => r.Read()).Returns(true);
                mockReader.Setup(r => r.IsDBNull(It.IsAny<int>())).Returns(true);
                mockContext.SetupSql<AggregateStats>(
                    AggregateStatsQuery.Sql,
                    mockReader,
                    connectionTimeout: 200,
                    behavior: CommandBehavior.CloseConnection | CommandBehavior.SingleRow);

                var query = new AggregateStatsQuery() { DatabaseContext = mockContext.Object };

                // Act
                var result = query.Execute();

                // Assert
                Assert.Equal(new AggregateStats(), result);
            }

            [Fact]
            public void GivenData_ItShouldReturnAPropertyFilledAggregateStats()
            {
                // Arrange
                var expected = new AggregateStats() {
                    Downloads = 42,
                    TotalPackages = 240,
                    UniquePackages = 24
                };

                var mockContext = new Mock<IEntitiesContext>();
                var mockReader = new Mock<IDataReader>();
                mockReader.Setup(r => r.Read()).Returns(true);
                mockReader.Setup(r => r.IsDBNull(It.IsAny<int>())).Returns(false);
                mockReader.Setup(r => r.GetInt32(0)).Returns(expected.UniquePackages);
                mockReader.Setup(r => r.GetInt32(1)).Returns(expected.TotalPackages);
                mockReader.Setup(r => r.GetInt64(2)).Returns(expected.Downloads);

                mockContext.SetupSql<AggregateStats>(
                    AggregateStatsQuery.Sql,
                    mockReader,
                    connectionTimeout: 200,
                    behavior: CommandBehavior.CloseConnection | CommandBehavior.SingleRow);

                var query = new AggregateStatsQuery() { DatabaseContext = mockContext.Object };

                // Act
                var result = query.Execute();

                // Assert
                Assert.Equal(expected, result);
            }
        }
    }
}
