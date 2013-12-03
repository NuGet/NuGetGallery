using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Services.Jobs
{
    public class JobDispatcherFacts
    {
        public class TheDispatchMethod
        {
            [Fact]
            public async Task GivenNoJobWithName_ItThrowsUnknownJobException()
            {
                // Arrange
                var dispatcher = new JobDispatcher(ServiceConfiguration.Create(), Enumerable.Empty<JobDescription>(), monitor: null);
                var invocation = new Invocation(Guid.NewGuid(), "flarg", "test", new Dictionary<string, string>());
                var context = new InvocationContext(new InvocationRequest(invocation), queue: null, config: null, logCapture: null);

                // Act/Assert
                var ex = await AssertEx.Throws<UnknownJobException>(() => dispatcher.Dispatch(context));
                Assert.Equal("flarg", ex.JobName);
            }

            [Fact]
            public async Task GivenJobWithName_ItCreatesAnInvocationAndInvokesJob()
            {
                // Arrange
                var jobImpl = new Mock<JobBase>();
                var job = new JobDescription("test", "blarg", () => jobImpl.Object);

                var dispatcher = new JobDispatcher(ServiceConfiguration.Create(), new[] { job }, monitor: null);
                var invocation = new Invocation(Guid.NewGuid(), "Test", "test", new Dictionary<string, string>());
                var context = new InvocationContext(new InvocationRequest(invocation), queue: null, config: null, logCapture: null);

                jobImpl.Setup(j => j.Invoke(It.IsAny<InvocationContext>()))
                   .Returns(Task.FromResult(InvocationResult.Completed()));


                // Act
                var result = await dispatcher.Dispatch(context);

                // Assert
                Assert.Equal(InvocationStatus.Completed, result.Status);
            }

            [Fact]
            public async Task GivenJobWithName_ItReturnsResponseContainingInvocationAndResult()
            {
                // Arrange
                var jobImpl = new Mock<JobBase>();
                var job = new JobDescription("test", "blarg", () => jobImpl.Object);
                
                var ex = new Exception();
                var dispatcher = new JobDispatcher(ServiceConfiguration.Create(), new[] { job }, monitor: null);
                var invocation = new Invocation(Guid.NewGuid(), "Test", "test", new Dictionary<string, string>());
                var context = new InvocationContext(new InvocationRequest(invocation), queue: null, config: null, logCapture: null);

                jobImpl.Setup(j => j.Invoke(It.IsAny<InvocationContext>()))
                   .Returns(Task.FromResult(InvocationResult.Completed()));

                // Act
                var result = await dispatcher.Dispatch(context);

                // Assert
                Assert.Equal(InvocationStatus.Completed, result.Status);
                Assert.Null(result.Exception);
            }
        }
    }
}
