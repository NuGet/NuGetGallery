using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Jobs;
using Xunit;

namespace NuGetGallery.Backend
{
    public class JobDispatcherFacts
    {
        public class TheDispatchMethod
        {
            [Fact]
            public async Task GivenNoJobWithName_ItThrowsUnknownJobException()
            {
                // Arrange
                var dispatcher = new JobDispatcher(BackendConfiguration.Create(), Enumerable.Empty<Job>());
                var request = new JobRequest("flarg", "test", new Dictionary<string, string>());
                var invocation = new JobInvocation(Guid.NewGuid(), request, DateTimeOffset.UtcNow);

                // Act/Assert
                var ex = await AssertEx.Throws<UnknownJobException>(() => dispatcher.Dispatch(invocation));
                Assert.Equal("flarg", ex.JobName);
            }

            [Fact]
            public async Task GivenJobWithName_ItCreatesAnInvocationAndInvokesJob()
            {
                // Arrange
                var job = new Mock<Job>();
                job.Setup(j => j.Name).Returns("Test");
                
                var dispatcher = new JobDispatcher(BackendConfiguration.Create(), new[] { job.Object });
                var request = new JobRequest("Test", "test", new Dictionary<string, string>());
                var invocation = new JobInvocation(Guid.NewGuid(), request, DateTimeOffset.UtcNow);

                job.Setup(j => j.Invoke(It.IsAny<JobInvocationContext>()))
                   .Returns(Task.FromResult(JobResult.Completed()));


                // Act
                var response = await dispatcher.Dispatch(invocation);

                // Assert
                Assert.Same(invocation, response.Invocation);
                Assert.Equal(JobResult.Completed(), response.Result);
            }

            [Fact]
            public async Task GivenJobWithName_ItReturnsResponseContainingInvocationAndResult()
            {
                // Arrange
                var job = new Mock<Job>();
                job.Setup(j => j.Name).Returns("Test");
                
                var ex = new Exception();
                var dispatcher = new JobDispatcher(BackendConfiguration.Create(), new[] { job.Object });
                var request = new JobRequest("Test", "test", new Dictionary<string, string>());
                var invocation = new JobInvocation(Guid.NewGuid(), request, DateTimeOffset.UtcNow);

                job.Setup(j => j.Invoke(It.IsAny<JobInvocationContext>()))
                   .Returns(Task.FromResult(JobResult.Faulted(ex)));

                // Act
                var response = await dispatcher.Dispatch(invocation);

                // Assert
                Assert.Same(invocation, response.Invocation);
                Assert.Equal(JobResult.Faulted(ex), response.Result);
            }
        }
    }
}
