using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.Factory;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.App_Start
{
    public class ConfigObjectFactoryFacts
    {
        [Fact]
        public async void FetchesCorrectlyAndRefreshesCachedObject()
        {
            // Arrange
            var value = "i am a key";

            var configTuple = CreateConfigService();
            configTuple.Item2.Setup(x => x.AppInsightsInstrumentationKey).Returns(value);

            var factory = new ConfigObjectDelegate<string>(objects => (string)objects[0], "AppInsightsInstrumentationKey");

            // Act
            var result = await factory.GetAsync(configTuple.Item1.Object);
            var result2 = await factory.GetAsync(configTuple.Item1.Object);

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(result, result2);
            
            // Arrange 2
            configTuple.Item2.Setup(x => x.ReadOnlyMode).Returns(true);

            // Act 2
            result = await factory.GetAsync(configTuple.Item1.Object);
            result2 = await factory.GetAsync(configTuple.Item1.Object);

            // Assert 2
            Assert.Equal(value, result);
            Assert.Equal(result, result2);
        }

        [Fact]
        public void DoesNotThrowWhenCreateIsCalledBeforeCreateAsync()
        {
            // Arrange
            var value = "hello i am a key";

            var configTuple = CreateConfigService();
            configTuple.Item2.Setup(x => x.AppInsightsInstrumentationKey).Returns(value);

            var factory = new ConfigObjectDelegate<string>(objects => (string)objects[0], "AppInsightsInstrumentationKey");

            // Act
            var result = factory.Get(configTuple.Item1.Object);

            // Assert
            Assert.Equal(value, result);
        }

        private T SleepAndReturn<T>(T value, int duration)
        {
            Thread.Sleep(duration);
            return value;
        }

        [Fact]
        public async void SimultaneousCreateAsyncCallsUseSameTask()
        {
            // Arrange
            var value = "hello i am a key";

            var configTuple = CreateConfigService();
            configTuple.Item2.Setup(x => x.AppInsightsInstrumentationKey).Returns(() => SleepAndReturn(value, 100));

            var factory = new ConfigObjectDelegate<string>(objects => (string)objects[0], "AppInsightsInstrumentationKey");

            // Act
            var firstTask = factory.GetAsync(configTuple.Item1.Object);
            var secondTask = factory.GetAsync(configTuple.Item1.Object);

            // Assert
            Assert.Equal(secondTask, firstTask);

            // Act 2
            var result = await firstTask;
            var result2 = await secondTask;

            // Assert 2
            Assert.Equal(value, result);
            Assert.Equal(result, result2);
        }

        [Fact]
        public async void CreateReturnsCachedValueAndDoesNotBlock()
        {
            // Arrange
            var value = "hello i am a key";
            var value2 = "hi i'm another key";

            var configTuple = CreateConfigService();
            configTuple.Item2.Setup(x => x.AppInsightsInstrumentationKey).Returns(() => SleepAndReturn(value, 100));

            var factory = new ConfigObjectDelegate<string>(objects => (string)objects[0], "AppInsightsInstrumentationKey");

            // Act

            // Guarantee that a value has been cached.
            var initial = factory.Get(configTuple.Item1.Object);

            // Switch value, but it hasn't been cached yet.
            configTuple.Item2.Setup(x => x.AppInsightsInstrumentationKey).Returns(() => SleepAndReturn(value2, 100));

            var cached = factory.Get(configTuple.Item1.Object);
            var final = await factory.GetAsync(configTuple.Item1.Object);

            // Assert
            Assert.Equal(value, initial);
            Assert.Equal(initial, cached);

            Assert.Equal(value2, final);
        }

        [Fact]
        public async void CreateHandlesOngoingCreateAsync()
        {
            // Arrange
            var value = "hello i am a key";
            var value2 = "hi i'm another key";

            var configTuple = CreateConfigService();
            configTuple.Item2.Setup(x => x.AppInsightsInstrumentationKey).Returns(() => SleepAndReturn(value, 100));

            var factory = new ConfigObjectDelegate<string>(objects => (string)objects[0], "AppInsightsInstrumentationKey");

            // Act

            // Guarantee that a value has been cached.
            var initial = factory.Get(configTuple.Item1.Object);

            // Switch value, but it hasn't been cached yet.
            configTuple.Item2.Setup(x => x.AppInsightsInstrumentationKey).Returns(() => SleepAndReturn(value2, 100));

            var finalTask = Task.Run(() => factory.GetAsync(configTuple.Item1.Object));
            var cached = factory.Get(configTuple.Item1.Object);
            var final = await finalTask;

            // Assert
            Assert.Equal(value, initial);
            Assert.Equal(initial, cached);
            Assert.Equal(value2, final);
        }

        private Tuple<Mock<IGalleryConfigurationService>, Mock<IAppConfiguration>> CreateConfigService()
        {
            var mockConfigService = new Mock<IGalleryConfigurationService>();

            var mockConfig = new Mock<IAppConfiguration>();
            mockConfigService.Setup(x => x.GetCurrent()).Returns(Task.FromResult(mockConfig.Object));
            mockConfigService.Setup(x => x.Current).Returns(mockConfig.Object);

            return Tuple.Create(mockConfigService, mockConfig);
        }
    }
}
