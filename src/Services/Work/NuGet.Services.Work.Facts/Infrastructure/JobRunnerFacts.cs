using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using NuGet.Services.Configuration;
using NuGet.Services.Work.Monitoring;
using NuGet.Services.Storage;
using Xunit;
using Xunit.Extensions;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work.Infrastructure
{
    public class JobRunnerFacts
    {
        public class TheRunMethod
        {
            [Fact]
            public async Task DequeuesMessageFromQueue()
            {
                // Arrange
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5), skipDispatch: true);
                var cts = new CancellationTokenSource();
                var dequeueTcs = runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .WaitsForSignal();

                // Act
                var task = runner.Run(cts.Token);

                // Assert
                runner.MockQueue.Verify(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token));

                // Abort the waiting threads
                dequeueTcs.SetException(new OperationCanceledException());
                cts.Cancel();
                try
                {
                    await task;
                }
                catch (TaskCanceledException) { }
            }

            [Fact]
            public async Task PutsServiceInDequeingStateUntilQueueReturnsMessage()
            {
                // Arrange
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5), skipDispatch: true);
                var cts = new CancellationTokenSource();
                var dequeueTcs = runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .WaitsForSignal();
                RunnerStatus statusAtHeartBeat = RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtHeartBeat = runner.Status;

                // Act
                var task = runner.Run(cts.Token);

                // Assert
                Assert.Equal(RunnerStatus.Dequeuing, runner.Status);
                Assert.Equal(RunnerStatus.Dequeuing, statusAtHeartBeat);

                // Abort the waiting threads
                dequeueTcs.SetException(new OperationCanceledException());
                cts.Cancel();
                try
                {
                    await task;
                }
                catch (TaskCanceledException) { }
            }

            [Fact]
            public async Task PutsServiceInDispatchingStateWhenQueueReturnsMessage()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5), skipDispatch: true);
                var dequeueTcs = new TaskCompletionSource<InvocationState>();

                runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .Returns(async () =>
                    {
                        var result = await dequeueTcs.Task;
                        dequeueTcs = new TaskCompletionSource<InvocationState>();
                        return result;
                    });
                RunnerStatus statusAtHeartBeat = RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtHeartBeat = runner.Status;

                // Act
                var task = runner.Run(cts.Token);
                dequeueTcs.SetResult(TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>()));

                // Assert
                Assert.Equal(RunnerStatus.Dispatching, runner.Status);
                Assert.Equal(RunnerStatus.Dispatching, statusAtHeartBeat);

                // Abort the waiting threads
                cts.Cancel();
                runner.DispatchTCS.TrySetResult(null);
                dequeueTcs.SetResult(null);
                try
                {
                    await task;
                }
                catch (TaskCanceledException) { }
            }

            [Fact]
            public async Task DispatchesRequestIfOneIsReceived()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5), skipDispatch: true);
                var dequeueTcs = runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .WaitsForSignal();
                var request = TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());

                // Act
                var task = runner.Run(cts.Token);
                dequeueTcs.SetResult(request);
                cts.Cancel();
                runner.DispatchTCS.TrySetResult(null);

                // Abort the waiting threads
                try
                {
                    await task;
                }
                catch (TaskCanceledException) { }

                // Assert
                Assert.Same(request, runner.LastDispatched);
            }

            [Fact]
            public async Task SleepsForPollIntervalWhenDequeueReturnsNoMessage()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5), skipDispatch: true);
                var dequeueTcs = new TaskCompletionSource<InvocationState>();

                runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .Returns(async () =>
                    {
                        var result = await dequeueTcs.Task;
                        dequeueTcs = new TaskCompletionSource<InvocationState>();
                        return result;
                    });
                RunnerStatus statusAtLastHeartBeat = RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtLastHeartBeat = runner.Status;
                dequeueTcs.SetResult(null);
                runner.DispatchTCS.TrySetResult(null);

                // Run to the sleep
                var task = runner.Run(cts.Token);
                runner.DispatchTCS = new TaskCompletionSource<object>();

                // Act
                runner.VirtualClock.Advance(TimeSpan.FromSeconds(5));

                // Assert
                Assert.Equal(RunnerStatus.Dequeuing, runner.Status);
                Assert.Equal(RunnerStatus.Dequeuing, statusAtLastHeartBeat);

                // Abort the waiting threads
                runner.DispatchTCS.SetResult(null);
                dequeueTcs.SetResult(null);
                cts.Cancel();
                try
                {
                    await task;
                }
                catch (TaskCanceledException) { }
            }

            [Fact]
            public async Task PutsServiceIntoSleepingStateWhenDequeueReturnsNoMessage()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5), skipDispatch: true);
                var dequeueTcs = runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .WaitsForSignal();
                RunnerStatus statusAtLastHeartBeat = RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtLastHeartBeat = runner.Status;
                dequeueTcs.SetResult(null);

                // Act, run the task to the sleep
                var task = runner.Run(cts.Token);

                // Assert
                Assert.Equal(RunnerStatus.Sleeping, runner.Status);
                Assert.Equal(RunnerStatus.Sleeping, statusAtLastHeartBeat);

                // Abort the waiting threads
                runner.VirtualClock.Advance(TimeSpan.FromMinutes(10));
                cts.Cancel();
                try
                {
                    await task;
                }
                catch (TaskCanceledException) { }
            }

            [Fact]
            public async Task PutsServiceIntoStoppingStatusWhenCancellationRequested()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5), skipDispatch: true);
                var dequeueTcs = runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .WaitsForSignal();
                cts.Cancel();
                RunnerStatus statusAtLastHeartBeat = RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtLastHeartBeat = runner.Status;

                // Act
                var task = runner.Run(cts.Token);

                // Assert
                Assert.Equal(RunnerStatus.Stopping, runner.Status);
                Assert.Equal(RunnerStatus.Stopping, statusAtLastHeartBeat);

                // Abort the waiting threads
                try
                {
                    await task;
                }
                catch (TaskCanceledException) { }
            }
        }

        public class TheDispatchMethod
        {
            [Fact]
            public async Task UpdatesTheStatusOfTheRequestToExecuting()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                
                var dispatchTCS = runner
                    .MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .WaitsForSignal();

                // Act
                var task = runner.Dispatch(invocation, cts.Token);

                // Assert
                runner.MockQueue.Verify(q => q.UpdateStatus(invocation, InvocationStatus.Executing, ExecutionResult.Incomplete));

                dispatchTCS.SetResult(InvocationResult.Completed());

                await task;
            }

            [Fact]
            public async Task DispatchesTheJobUsingTheDispatcher()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                
                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(InvocationResult.Completed())
                    .Verifiable();

                // Act
                await runner.Dispatch(invocation, cts.Token);

                // Assert
                runner.MockDispatcher.VerifyAll();
            }

            [Fact]
            public async Task AcknowledgesMessageOnCompletionOfInvocation()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                
                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(InvocationResult.Completed())
                    .Verifiable();

                // Act
                await runner.Dispatch(invocation, cts.Token);

                // Assert
                runner.MockQueue.Verify(q => q.Complete(invocation, ExecutionResult.Completed, null, null));
            }

            [Theory]
            [InlineData(ExecutionResult.Faulted)]
            [InlineData(ExecutionResult.Crashed)]
            public async Task CompletesWithResultMessageIfThereIsAnException(ExecutionResult result)
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var exception = new Exception("BORK!");
                
                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(() =>
                    {
                        runner.VirtualClock.Advance(TimeSpan.FromDays(365)); // Wow this is a long job ;)
                        return new InvocationResult(result, exception);
                    })
                    .Verifiable();

                // Act
                await runner.Dispatch(invocation, cts.Token);

                // Assert
                runner.MockQueue.Verify(q => q.Complete(invocation, result, exception.ToString(), null));
            }

            [Fact]
            public async Task SuspendsInvocationIfJobRemainsIncompleteWithAContinuation()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                
                var continuationPayload = new Dictionary<string, string>() { { "foo", "bar" } };
                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(InvocationResult.Suspended(new JobContinuation(TimeSpan.FromDays(365), continuationPayload)))
                    .Verifiable();

                // Act
                await runner.Dispatch(invocation, cts.Token);

                // Assert
                runner.MockQueue.Verify(q => q.Suspend(invocation, continuationPayload, TimeSpan.FromDays(365), null));
            }

            [Theory]
            [InlineData(ExecutionResult.Completed)]
            [InlineData(ExecutionResult.Faulted)]
            public async Task EnqueuesARescheduleIfTheResultRequestsIt(ExecutionResult result)
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var repeat = TestHelpers.CreateInvocation(Guid.NewGuid(), "test", "test", new Dictionary<string,string>());
                
                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(new InvocationResult(result, result == ExecutionResult.Faulted ? new Exception() : null, TimeSpan.FromDays(365)))
                    .Verifiable();
                runner.MockQueue
                    .Setup(q => q.Enqueue("test", Constants.Source_RepeatingJob, It.IsAny<Dictionary<string, string>>(), TimeSpan.FromDays(365)))
                    .Completes(repeat);

                // Act
                await runner.Dispatch(invocation, cts.Token);

                // Assert
                runner.MockQueue.Verify(q => q.Enqueue(invocation.Job, Constants.Source_RepeatingJob, invocation.Payload, TimeSpan.FromDays(365)));
            }
        }

        internal class TestableJobRunner : JobRunner
        {
            private bool _skipDispatch;

            public Mock<InvocationQueue> MockQueue { get; private set; }
            public Mock<JobDispatcher> MockDispatcher { get; private set; }
            public Mock<StorageHub> MockStorage { get; private set; }
            public VirtualClock VirtualClock { get; private set; }

            public InvocationState LastDispatched { get; private set; }
            public TaskCompletionSource<object> DispatchTCS { get; set; }

            public TestableJobRunner(TimeSpan pollInterval, bool skipDispatch = false)
                : base(pollInterval)
            {
                // Arrange
                Queue = (MockQueue = new Mock<InvocationQueue>()).Object;
                Dispatcher = (MockDispatcher = new Mock<JobDispatcher>()).Object;
                Storage = (MockStorage = new Mock<StorageHub>()).Object;
                Clock = VirtualClock = new VirtualClock();

                _skipDispatch = skipDispatch;
                DispatchTCS = new TaskCompletionSource<object>();

                // Set up things so that async methods don't return null Tasks
                MockQueue
                    .Setup(q => q.UpdateStatus(It.IsAny<InvocationState>(), It.IsAny<InvocationStatus>(), It.IsAny<ExecutionResult>()))
                    .Completes(true);
                MockQueue
                    .Setup(q => q.Complete(It.IsAny<InvocationState>(), It.IsAny<ExecutionResult>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Completes(true);
                MockQueue
                    .Setup(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>()))
                    .Completes((InvocationState)null);
                MockQueue
                    .Setup(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<TimeSpan>()))
                    .Completes((InvocationState)null);
                MockQueue
                    .Setup(q => q.Suspend(It.IsAny<InvocationState>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<TimeSpan>(), It.IsAny<string>()))
                    .Completes(true);
            }

            protected internal override Task Dispatch(InvocationState invocation, CancellationToken cancelToken)
            {
                return Dispatch(invocation, new InvocationLogCapture(invocation), cancelToken);
            }

            protected internal override Task Dispatch(InvocationState invocation, InvocationLogCapture capture, CancellationToken cancelToken)
            {
                LastDispatched = invocation;
                if (_skipDispatch)
                {
                    return DispatchTCS.Task;
                }
                else
                {
                    return base.Dispatch(invocation, capture, cancelToken);
                }
            }
        }
    }
}
