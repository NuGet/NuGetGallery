using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Work.Models;
using Xunit;

namespace NuGet.Services.Work
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
                var invocation = TestHelpers.CreateInvocation(
                    Guid.NewGuid(), 
                    "Test",
                    "Test");
                var context = new InvocationContext(invocation, queue: null);

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
                var invocation = TestHelpers.CreateInvocation(
                    Guid.NewGuid(), 
                    "Test",
                    "Test",
                    new Dictionary<string, string>()
                    {
                        {"TestParameter", "frob"},
                        {"NotMapped", "bar"}
                    });
                var context = new InvocationContext(invocation, queue: null);

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
                var invocation = TestHelpers.CreateInvocation(
                    Guid.NewGuid(),
                    "Test",
                    "Test",
                    new Dictionary<string, string>()
                    {
                        {"ConvertValue", "frob"},
                    });
                var context = new InvocationContext(invocation, queue: null);

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
                var invocation = TestHelpers.CreateInvocation(
                    Guid.NewGuid(),
                    "Jerb",
                    "Test");
                var context = new InvocationContext(invocation, queue: null);

                // Act
                var result = await job.Object.Invoke(context);

                // Assert
                Assert.Equal(ExecutionResult.Completed, result.Result);
            }

            [Fact]
            public async Task GivenAJobThrows_ItReturnsFaultedJobResult()
            {
                // Arrange
                var job = new Mock<TestJob>() { CallBase = true };
                var invocation = TestHelpers.CreateInvocation(
                    Guid.NewGuid(),
                    "Jerb",
                    "Test");
                var ex = new NotImplementedException("Broked!");
                job.Setup(j => j.Execute()).Throws(ex);
                var context = new InvocationContext(invocation, queue: null);

                // Act
                var result = await job.Object.Invoke(context);

                // Assert
                Assert.Equal(ExecutionResult.Faulted, result.Result);
                Assert.Equal(ex, result.Exception);
            }
        }

        public class TestJob : JobHandler<TestJobEventSource>
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
