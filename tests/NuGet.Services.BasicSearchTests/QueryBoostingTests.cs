using Newtonsoft.Json;
using NuGet.Indexing;
using NuGet.Services.BasicSearchTests.TestSupport;
using NuGet.Services.Logging;
using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.BasicSearchTests
{
    public class QueryBoostingTests
    {
        [Fact]
        public void TestDefaultQueryBoostingContextCanBeLoaded()
        {
            // Arrange
            var defaultSerializedContext = JsonConvert.SerializeObject(QueryBoostingContext.Default);
            var result = Load(defaultSerializedContext);

            // Assert
            Assert.Equal(QueryBoostingContext.Default.BoostByDownloads, result.BoostByDownloads);
            Assert.Equal(QueryBoostingContext.Default.Threshold, result.Threshold);
            Assert.Equal(QueryBoostingContext.Default.Factor, result.Factor);
        }

        [Fact]
        public void TestValidJsonCanBeLoaded()
        {
            // Arrange
            var first = Load("{'boostByDownloads': 'true', 'threshold': 2000, 'factor': 40}");
            var second = Load("{'boostByDownloads': 'false', 'threshold': 3000 }");
            var third = Load("{'boostByDownloads': 'true' }");
            var fourth = Load("{}");

            // Assert
            Assert.True(first.BoostByDownloads);
            Assert.Equal(2000, first.Threshold);
            Assert.Equal(40f, first.Factor);

            Assert.False(second.BoostByDownloads);
            Assert.Equal(3000, second.Threshold);
            Assert.Equal(0.1f, second.Factor);

            Assert.True(third.BoostByDownloads);
            Assert.Equal(1, third.Threshold);
            Assert.Equal(0.1f, third.Factor);

            Assert.True(third.BoostByDownloads);
            Assert.Equal(1, third.Threshold);
            Assert.Equal(0.1f, third.Factor);
        }

        [Fact]
        public void TestEmptyContextThrowsExceptionOnLoad()
        {
            // Loading the default context should not throw any exceptions.
            Assert.Throws<InvalidOperationException>(() => Load(""));
            Assert.Throws<JsonReaderException>(() => Load("Hello world"));
        }

        [Fact]
        public void TestInvalidFileNameThrowsExceptionOnLoad()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                Load("a.json", new InMemoryLoader());
            });

            Assert.Throws<KeyNotFoundException>(() =>
            {
                Load("a.json", new InMemoryLoader
                {
                    { "b.json", JsonConvert.SerializeObject(QueryBoostingContext.Default) },
                });
            });
        }

        private QueryBoostingContext Load(string content)
        {
            var configFileName = "queryContext.json";

            return Load(configFileName, new InMemoryLoader
            {
                { configFileName, content },
            });
        }

        private QueryBoostingContext Load(string configFileName, InMemoryLoader loader)
        {
            var logger = LoggingSetup.CreateLoggerFactory().CreateLogger(nameof(QueryBoostingContext));

            return QueryBoostingContext.Load(configFileName, loader, logger);
        }
    }
}