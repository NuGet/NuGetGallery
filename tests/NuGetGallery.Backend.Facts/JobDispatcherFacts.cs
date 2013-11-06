using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery.Backend
{
    public class JobDispatcherFacts
    {
        public class TheDispatchMethod
        {
            [Fact]
            public void GivenNoJobWithName_ItThrowsUnknownJobException()
            {
                // Arrange
                var dispatcher = new JobDispatcher(BackendConfiguration.Create(), Enumerable.Empty<Job>());
                var request = new JobRequest("flarg", new Dictionary<string, string>());

                // Act/Assert
                var ex = Assert.Throws<UnknownJobException>(() => dispatcher.Dispatch(request));
                Assert.Equal("flarg", ex.JobName);
            }

            [Fact]
            public void GivenJobWithName_ItCreatesAnInvocationAndInvokesJob()
            {
                // Arrange
                var job = new Mock<Job>();
                JobInvocation invocation = null;
                job.Setup(j => j.Name).Returns("Test");
                job.Setup(j => j.Invoke(It.IsAny<JobInvocation>()))
                   .Returns<JobInvocation>(i =>
                   {
                       invocation = i;
                       return JobResult.Completed();
                   });

                var dispatcher = new JobDispatcher(BackendConfiguration.Create(), new[] { job.Object });
                var request = new JobRequest("Test", new Dictionary<string, string>());

                // Act
                dispatcher.Dispatch(request);

                // Assert
                Assert.NotNull(invocation);
                Assert.Same(request, invocation.Request);
                Assert.Equal("Dispatcher", invocation.Source);
            }

            [Fact]
            public void GivenJobWithName_ItReturnsResponseContainingInvocationAndResult()
            {
                // Arrange
                var job = new Mock<Job>();
                var ex = new Exception();
                JobInvocation invocation = null;
                job.Setup(j => j.Name).Returns("Test");
                job.Setup(j => j.Invoke(It.IsAny<JobInvocation>()))
                   .Returns<JobInvocation>(i =>
                   {
                       invocation = i;
                       return JobResult.Faulted(ex);
                   });

                var dispatcher = new JobDispatcher(BackendConfiguration.Create(), new[] { job.Object });
                var request = new JobRequest("Test", new Dictionary<string, string>());

                // Act
                var response = dispatcher.Dispatch(request);

                // Assert
                Assert.Same(invocation, response.Invocation);
                Assert.Equal(JobResult.Faulted(ex), response.Result);
            }
        }
    }
}
