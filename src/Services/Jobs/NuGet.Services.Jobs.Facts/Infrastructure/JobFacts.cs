using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Services.Jobs
{
    public class JobFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public async Task GivenAnInvocation_ItSetsTheInvocationProperty()
            {
                // Arrange
                var job = new TestJob();
                var invocation = new Invocation(
                    Guid.NewGuid(), 
                    "Test",
                    "Test",
                    new Dictionary<string, string>());
                var context = new InvocationContext(new InvocationRequest(invocation), queue: null, logCapture: null);

                // Act
                await job.Invoke(context);

                // Assert
                Assert.Same(invocation, job.Invocation);
            }

            [Fact]
            public async Task GivenParametersThatMatchPropertyNames_ItSetsPropertiesToThoseValues()
            {
                // Arrange
                var job = new TestJob();
                var invocation = new Invocation(
                    Guid.NewGuid(), 
                    "Test",
                    "Test",
                    new Dictionary<string, string>()
                    {
                        {"TestParameter", "frob"},
                        {"NotMapped", "bar"}
                    });
                var context = new InvocationContext(new InvocationRequest(invocation), queue: null, logCapture: null);

                // Act
                await job.Invoke(context);

                // Assert
                Assert.Equal("frob", job.TestParameter);
            }

            [Fact]
            public async Task GivenPropertiesWithConverters_ItUsesTheConverterToChangeTheValue()
            {
                // Arrange
                var job = new TestJob();
                var invocation = new Invocation(
                    Guid.NewGuid(),
                    "Test",
                    "Test",
                    new Dictionary<string, string>()
                    {
                        {"ConvertValue", "frob"},
                    });
                var context = new InvocationContext(new InvocationRequest(invocation), queue: null, logCapture: null);

                // Act
                await job.Invoke(context);

                // Assert
                Assert.Equal("http://it.was.a.string/frob", job.ConvertValue.AbsoluteUri);
            }

            [Fact]
            public async Task GivenAJobExecutesWithoutException_ItReturnsCompletedJobResult()
            {
                // Arrange
                var job = new Mock<TestJob>() { CallBase = true };
                var invocation = new Invocation(
                    Guid.NewGuid(),
                    "Jerb",
                    "Test",
                    new Dictionary<string, string>());
                var context = new InvocationContext(new InvocationRequest(invocation), queue: null, logCapture: null);

                // Act
                var result = await job.Object.Invoke(context);

                // Assert
                Assert.Equal(InvocationStatus.Completed, result.Status);
            }

            [Fact]
            public async Task GivenAJobThrows_ItReturnsFaultedJobResult()
            {
                // Arrange
                var job = new Mock<TestJob>() { CallBase = true };
                var invocation = new Invocation(
                    Guid.NewGuid(),
                    "Jerb",
                    "Test",
                    new Dictionary<string, string>());
                var ex = new NotImplementedException("Broked!");
                job.Setup(j => j.Execute()).Throws(ex);
                var context = new InvocationContext(new InvocationRequest(invocation), queue: null, logCapture: null);

                // Act
                var result = await job.Object.Invoke(context);

                // Assert
                Assert.Equal(InvocationStatus.Faulted, result.Status);
                Assert.Equal(ex, result.Exception);
            }
        }

        public class TestJob : Job<TestJobEventSource>
        {
            public string TestParameter { get; set; }

            [TypeConverter(typeof(TestUriConverter))]
            public Uri ConvertValue { get; set; }

            protected internal override Task Execute()
            {
                return Task.FromResult<object>(null);
            }
        }

        public class TestUriConverter : TypeConverter
        {
            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                if (value is string)
                {
                    return new Uri("http://it.was.a.string/" + (string)value);
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        public class TestJobEventSource : EventSource
        {
            public static readonly TestJobEventSource Log = new TestJobEventSource();
            private TestJobEventSource() { }
        }
    }
}
