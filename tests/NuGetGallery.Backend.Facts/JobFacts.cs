using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery.Backend
{
    public class JobFacts
    {
        public class TheNameProperty
        {
            [Fact]
            public void GivenAJobWithClassNameEndingJob_ItReturnsThePartBeforeTheWordJob()
            {
                Assert.Equal("Test", new TestJob().Name);
            }

            [Fact]
            public void GivenAJobWithClassNameNotEndingJob_ItReturnsTheWholeTypeName()
            {
                Assert.Equal("ATestJerb", new ATestJerb().Name);
            }

            public class ATestJerb : Job
            {
                protected internal override void Execute()
                {
                }

                public override EventSource GetEventSource()
                {
                    return TestJobEventSource.Log;
                }
            }
        }

        public class TheExecuteMethod
        {
            [Fact]
            public void GivenAnInvocation_ItSetsTheInvocationProperty()
            {
                // Arrange
                var job = new TestJob();
                var invocation = new JobInvocation(
                    Guid.NewGuid(), 
                    new JobRequest(
                        "Test",
                        "Test",
                        new Dictionary<string, string>()), 
                    DateTimeOffset.UtcNow, 
                    "test",
                    BackendConfiguration.Create());

                // Act
                job.Invoke(invocation);

                // Assert
                Assert.Same(invocation, job.Invocation);
            }

            [Fact]
            public void GivenParametersThatMatchPropertyNames_ItSetsPropertiesToThoseValues()
            {
                // Arrange
                var job = new TestJob();
                var invocation = new JobInvocation(
                    Guid.NewGuid(),
                    new JobRequest(
                        "Test",
                        "Test",
                        new Dictionary<string, string>()
                        {
                            {"TestParameter", "frob"},
                            {"NotMapped", "bar"}
                        }),
                    DateTimeOffset.UtcNow,
                    "test",
                    BackendConfiguration.Create());

                // Act
                job.Invoke(invocation);

                // Assert
                Assert.Equal("frob", job.TestParameter);
            }

            [Fact]
            public void GivenPropertiesWithConverters_ItUsesTheConverterToChangeTheValue()
            {
                // Arrange
                var job = new TestJob();
                var invocation = new JobInvocation(
                    Guid.NewGuid(),
                    new JobRequest(
                        "Test",
                        "Test",
                        new Dictionary<string, string>()
                        {
                            {"ConvertValue", "frob"},
                        }),
                    DateTimeOffset.UtcNow,
                    "test",
                    BackendConfiguration.Create());

                // Act
                job.Invoke(invocation);

                // Assert
                Assert.Equal("http://it.was.a.string/frob", job.ConvertValue.AbsoluteUri);
            }

            [Fact]
            public void GivenAJobExecutesWithoutException_ItReturnsCompletedJobResult()
            {
                // Arrange
                var job = new Mock<TestJob>() { CallBase = true };
                var invocation = new JobInvocation(
                    Guid.NewGuid(),
                    new JobRequest(
                        "Jerb",
                        "Test",
                        new Dictionary<string, string>()),
                    DateTimeOffset.UtcNow,
                    "test",
                    BackendConfiguration.Create());

                // Act
                var result = job.Object.Invoke(invocation);

                // Assert
                Assert.Equal(JobResult.Completed(), result);
            }

            [Fact]
            public void GivenAJobThrows_ItReturnsFaultedJobResult()
            {
                // Arrange
                var job = new Mock<TestJob>() { CallBase = true };
                var invocation = new JobInvocation(
                    Guid.NewGuid(),
                    new JobRequest(
                        "Jerb",
                        "Test",
                        new Dictionary<string, string>()),
                    DateTimeOffset.UtcNow,
                    "test",
                    BackendConfiguration.Create());
                var ex = new NotImplementedException("Broked!");
                job.Setup(j => j.Execute()).Throws(ex);
                
                // Act
                var result = job.Object.Invoke(invocation);

                // Assert
                Assert.Equal(JobResult.Faulted(ex), result);
            }
        }

        public class TestJob : Job
        {
            public string TestParameter { get; set; }

            [TypeConverter(typeof(TestUriConverter))]
            public Uri ConvertValue { get; set; }

            protected internal override void Execute()
            {
            }

            public override EventSource GetEventSource()
            {
                return TestJobEventSource.Log;
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

            private TestJobEventSource()  { }
        }
    }
}
