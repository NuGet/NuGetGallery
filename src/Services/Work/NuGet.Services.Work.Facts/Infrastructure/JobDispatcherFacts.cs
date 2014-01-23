using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Moq;
using NuGet.Services.TestInfrastructure;
using NuGet.Services.Work.Models;
using Xunit;

namespace NuGet.Services.Work
{
    public class JobDispatcherFacts
    {
        public class TheDispatchMethod
        {
            [Fact]
            public async Task GivenNoJobWithName_ItThrowsUnknownJobException()
            {
                // Arrange
                var host = new TestServiceHost();
                host.Initialize();

                var dispatcher = new JobDispatcher(Enumerable.Empty<JobDescription>(), host.Container);
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "flarg", "test", new Dictionary<string, string>());
                var context = new InvocationContext(invocation, queue: null);

                // Act/Assert
                var ex = await AssertEx.Throws<UnknownJobException>(() => dispatcher.Dispatch(context));
                Assert.Equal("flarg", ex.JobName);
            }

            [Fact]
            public async Task GivenJobWithName_ItInvokesTheJobAndReturnsTheResult()
            {
                // Arrange
                var host = new TestServiceHost();
                host.Initialize();

                var job = new JobDescription("test", typeof(TestJob));

                var dispatcher = new JobDispatcher(new[] { job }, host.Container);
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "Test", "test", new Dictionary<string, string>());
                var context = new InvocationContext(invocation, queue: null);
                var expected = InvocationResult.Completed();
                TestJob.SetTestResult(expected);

                // Act
                var actual = await dispatcher.Dispatch(context);

                // Assert
                Assert.Same(expected, actual);
            }

            [Fact]
            public async Task GivenJobWithConstructorArgs_ItResolvesThemFromTheContainer()
            {
                // Arrange
                var expected = new SomeService();
                var host = new TestServiceHost(
                    componentRegistrations: b => {
                        b.RegisterInstance(expected).As<SomeService>();
                    });
                host.Initialize();

                var job = new JobDescription("test", typeof(TestJobWithService));

                var dispatcher = new JobDispatcher(new[] { job }, host.Container);
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "Test", "test", new Dictionary<string, string>());
                var context = new InvocationContext(invocation, queue: null);
                var slot = new ContextSlot();
                TestJobWithService.SetContextSlot(slot);
                
                // Act
                await dispatcher.Dispatch(context);
                
                // Assert
                Assert.Same(expected, slot.Value);
            }

            [Fact]
            public async Task GivenAContinuationRequest_ItCallsTheInvokeContinuationMethod()
            {
                // Arrange
                var host = new TestServiceHost();
                host.Initialize();

                var job = new JobDescription("test", typeof(TestAsyncJob));

                var dispatcher = new JobDispatcher(new[] { job }, host.Container);
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "Test", "test", new Dictionary<string, string>(), isContinuation: true);
                var context = new InvocationContext(invocation, queue: null);
                
                // Act
                var result = await dispatcher.Dispatch(context);

                // Assert
                Assert.Equal(ExecutionResult.Completed, result.Result);
            }

            [Fact]
            public async Task GivenAContinuationRequestToANonAsyncJob_ItThrows()
            {
                // Arrange
                var host = new TestServiceHost();
                 host.Initialize();

                var job = new JobDescription("test", typeof(TestJob));

                var dispatcher = new JobDispatcher(new[] { job }, host.Container);
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "Test", "test", new Dictionary<string, string>(), isContinuation: true);
                var context = new InvocationContext(invocation, queue: null);

                // Act/Assert
                var ex = await AssertEx.Throws<InvalidOperationException>(() => dispatcher.Dispatch(context));
                Assert.Equal(String.Format(
                    Strings.JobDispatcher_AsyncContinuationOfNonAsyncJob,
                    job.Name), ex.Message);
            }
        }

        public class TestJob : JobHandlerBase
        {
            public override EventSource GetEventSource()
            {
                return null;
            }

            protected internal override Task<InvocationResult> Invoke()
            {
                return Task.FromResult(GetTestResult());
            }

            public static InvocationResult GetTestResult()
            {
                return (InvocationResult)CallContext.LogicalGetData(typeof(TestJob).FullName + "!TestResult");
            }

            public static void SetTestResult(InvocationResult value)
            {
                CallContext.LogicalSetData(typeof(TestJob).FullName + "!TestResult", value);
            }
        }

        public class TestAsyncJob : JobHandlerBase, IAsyncJob
        {
            public override EventSource GetEventSource()
            {
                return null;
            }

            public Task<InvocationResult> InvokeContinuation(InvocationContext context)
            {
                return Task.FromResult(InvocationResult.Completed());
            }

            protected internal override Task<InvocationResult> Invoke()
            {
                return Task.FromResult(InvocationResult.Faulted(new Exception("Supposed to be invoked as a continuation!")));
            }
        }

        public class TestJobWithService : JobHandlerBase
        {
            public TestJobWithService(SomeService service)
            {
                GetContextSlot().Value = service;
            }

            public override EventSource GetEventSource()
            {
                return null;
            }

            protected internal override Task<InvocationResult> Invoke()
            {
                return Task.FromResult(InvocationResult.Completed());
            }

            internal static ContextSlot GetContextSlot()
            {
                return (ContextSlot)CallContext.LogicalGetData(typeof(TestJobWithService).FullName + "!ContextSlot");
            }

            // Call context doesn't flow up the call stack, only down, but it's
            // a shallow copy (i.e. objects are copied as references), so we can
            // use a property on a dummy object to fake passing things up the call stack in the test
            internal static void SetContextSlot(ContextSlot value)
            {
                CallContext.LogicalSetData(typeof(TestJobWithService).FullName + "!ContextSlot", value);
            }
        }

        public class SomeService
        {
        }

        internal class ContextSlot
        {
            public SomeService Value { get; set; }
        }
    }
}
