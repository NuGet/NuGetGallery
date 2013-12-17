using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using NuGet.Services.Configuration;
using NuGet.Services.Jobs.Monitoring;
using NuGet.Services.Storage;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Services.Jobs.Infrastructure
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
                JobRunner.RunnerStatus statusAtHeartBeat = JobRunner.RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtHeartBeat = runner.Status;

                // Act
                var task = runner.Run(cts.Token);

                // Assert
                Assert.Equal(JobRunner.RunnerStatus.Dequeuing, runner.Status);
                Assert.Equal(JobRunner.RunnerStatus.Dequeuing, statusAtHeartBeat);

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
                var dequeueTcs = new TaskCompletionSource<InvocationRequest>();

                runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .Returns(async () =>
                    {
                        var result = await dequeueTcs.Task;
                        dequeueTcs = new TaskCompletionSource<InvocationRequest>();
                        return result;
                    });
                JobRunner.RunnerStatus statusAtHeartBeat = JobRunner.RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtHeartBeat = runner.Status;

                // Act
                var task = runner.Run(cts.Token);
                dequeueTcs.SetResult(new InvocationRequest(new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>()), new CloudQueueMessage("foo")));

                // Assert
                Assert.Equal(JobRunner.RunnerStatus.Dispatching, runner.Status);
                Assert.Equal(JobRunner.RunnerStatus.Dispatching, statusAtHeartBeat);

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
                var request = new InvocationRequest(new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>()), new CloudQueueMessage("foo"));

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
                var dequeueTcs = new TaskCompletionSource<InvocationRequest>();

                runner.MockQueue
                    .Setup(q => q.Dequeue(JobRunner.DefaultInvisibilityPeriod, cts.Token))
                    .Returns(async () =>
                    {
                        var result = await dequeueTcs.Task;
                        dequeueTcs = new TaskCompletionSource<InvocationRequest>();
                        return result;
                    });
                JobRunner.RunnerStatus statusAtLastHeartBeat = JobRunner.RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtLastHeartBeat = runner.Status;
                dequeueTcs.SetResult(null);
                runner.DispatchTCS.TrySetResult(null);

                // Run to the sleep
                var task = runner.Run(cts.Token);
                runner.DispatchTCS = new TaskCompletionSource<object>();

                // Act
                runner.VirtualClock.Advance(TimeSpan.FromSeconds(5));

                // Assert
                Assert.Equal(JobRunner.RunnerStatus.Dequeuing, runner.Status);
                Assert.Equal(JobRunner.RunnerStatus.Dequeuing, statusAtLastHeartBeat);

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
                JobRunner.RunnerStatus statusAtLastHeartBeat = JobRunner.RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtLastHeartBeat = runner.Status;
                dequeueTcs.SetResult(null);

                // Act, run the task to the sleep
                var task = runner.Run(cts.Token);

                // Assert
                Assert.Equal(JobRunner.RunnerStatus.Sleeping, runner.Status);
                Assert.Equal(JobRunner.RunnerStatus.Sleeping, statusAtLastHeartBeat);

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
                JobRunner.RunnerStatus statusAtLastHeartBeat = JobRunner.RunnerStatus.Working;
                runner.Heartbeat += (_, __) => statusAtLastHeartBeat = runner.Status;

                // Act
                var task = runner.Run(cts.Token);

                // Assert
                Assert.Equal(JobRunner.RunnerStatus.Stopping, runner.Status);
                Assert.Equal(JobRunner.RunnerStatus.Stopping, statusAtLastHeartBeat);

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
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));

                var dispatchTCS = runner
                    .MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .WaitsForSignal();

                // Act
                var task = runner.Dispatch(request, cts.Token);

                // Assert
                runner.MockQueue.Verify(q => q.Update(It.Is<Invocation>(i =>
                    i.LastDequeuedAt.Value == runner.VirtualClock.UtcNow &&
                    i.Status == InvocationStatus.Executing)));

                dispatchTCS.SetResult(InvocationResult.Completed());

                await task;
            }

            [Fact]
            public async Task DispatchesTheJobUsingTheDispatcher()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));

                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(InvocationResult.Completed())
                    .Verifiable();

                // Act
                await runner.Dispatch(request, cts.Token);

                // Assert
                runner.MockDispatcher.VerifyAll();
            }

            [Fact]
            public async Task AcknowledgesMessageOnCompletionOfInvocation()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));

                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(InvocationResult.Completed())
                    .Verifiable();

                // Act
                await runner.Dispatch(request, cts.Token);

                // Assert
                runner.MockQueue.Verify(q => q.Acknowledge(request));
            }

            [Theory]
            [InlineData(ExecutionResult.Completed)]
            [InlineData(ExecutionResult.Faulted)]
            [InlineData(ExecutionResult.Aborted)]
            [InlineData(ExecutionResult.Crashed)]
            public async Task SetsCompletedAtIfInvocationResultRepresentsFinalCompletion(ExecutionResult result)
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));

                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(() =>
                    {
                        runner.VirtualClock.Advance(TimeSpan.FromDays(365)); // Wow this is a long job ;)
                        return new InvocationResult(result, result == ExecutionResult.Faulted ? new Exception() : null);
                    })
                    .Verifiable();

                // Act
                await runner.Dispatch(request, cts.Token);

                // Assert
                Assert.Equal(InvocationStatus.Executed, invocation.Status);
                Assert.Equal(result, invocation.Result);
                Assert.False(invocation.LastSuspendedAt.HasValue);
                Assert.Equal(runner.VirtualClock.UtcNow, invocation.CompletedAt.Value);
                runner.MockQueue.Verify(q => q.Update(invocation));
            }

            [Fact]
            public async Task SetsLastSuspendedAtIfResultIsSuspended()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));

                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(() =>
                    {
                        runner.VirtualClock.Advance(TimeSpan.FromDays(365)); // Wow this is a long job ;)
                        return InvocationResult.Suspended(new JobContinuation(TimeSpan.FromSeconds(10), new Dictionary<string, string>()));
                    })
                    .Verifiable();

                // Act
                await runner.Dispatch(request, cts.Token);

                // Assert
                Assert.Equal(InvocationStatus.Executing, invocation.Status);
                Assert.Equal(ExecutionResult.Suspended, invocation.Result);
                Assert.False(invocation.CompletedAt.HasValue);
                runner.MockQueue.Verify(q => q.Update(invocation));
            }

            [Theory]
            [InlineData(ExecutionResult.Faulted)]
            [InlineData(ExecutionResult.Crashed)]
            public async Task SetsResultMessageIfThereIsAnException(ExecutionResult result)
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));

                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(() =>
                    {
                        runner.VirtualClock.Advance(TimeSpan.FromDays(365)); // Wow this is a long job ;)
                        return new InvocationResult(result, new Exception("BORK!"));
                    })
                    .Verifiable();

                // Act
                await runner.Dispatch(request, cts.Token);

                // Assert
                Assert.Equal(InvocationStatus.Executed, invocation.Status);
                Assert.Equal(result, invocation.Result);
                Assert.Equal("System.Exception: BORK!", invocation.ResultMessage);
                Assert.Equal(runner.VirtualClock.UtcNow, invocation.CompletedAt.Value);
                runner.MockQueue.Verify(q => q.Update(invocation));
            }

            [Fact]
            public async Task EnqueuesAContinuationIfTheInvocationIsSuspended()
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));

                Invocation enqueued = null;
                TimeSpan? visibleIn = null;
                var continuationPayload = new Dictionary<string, string>() { { "foo", "bar" } };
                runner.MockQueue
                    .Setup(q => q.Enqueue(It.IsAny<Invocation>(), It.IsAny<TimeSpan>()))
                    .Callback<Invocation, TimeSpan>((i, t) => { enqueued = i; visibleIn = t; })
                    .Completes();
                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(InvocationResult.Suspended(new JobContinuation(TimeSpan.FromDays(365), continuationPayload)))
                    .Verifiable();

                // Act
                await runner.Dispatch(request, cts.Token);

                // Assert
                Assert.NotNull(enqueued);
                Assert.Equal(invocation.Id, enqueued.Id);
                Assert.Equal(invocation.Job, enqueued.Job);
                Assert.Equal(Constants.Source_AsyncContinuation, enqueued.Source);
                Assert.Same(continuationPayload, enqueued.Payload);
                Assert.True(enqueued.Continuation);
                Assert.Equal(runner.VirtualClock.UtcNow, enqueued.LastSuspendedAt.Value);
                Assert.Equal(runner.VirtualClock.UtcNow + TimeSpan.FromDays(365), enqueued.EstimatedContinueAt.Value);
                Assert.Equal(TimeSpan.FromDays(365), visibleIn.Value);
            }

            [Theory]
            [InlineData(ExecutionResult.Completed)]
            [InlineData(ExecutionResult.Faulted)]
            public async Task EnqueuesARescheduleIfTheResultRequestsIt(ExecutionResult result)
            {
                // Arrange
                var cts = new CancellationTokenSource();
                var runner = new TestableJobRunner(TimeSpan.FromSeconds(5));
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string, string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));

                Invocation enqueued = null;
                TimeSpan? visibleIn = null;
                var continuationPayload = new Dictionary<string, string>() { { "foo", "bar" } };
                runner.MockQueue
                    .Setup(q => q.Enqueue(It.IsAny<Invocation>(), It.IsAny<TimeSpan>()))
                    .Callback<Invocation, TimeSpan>((i, t) => { enqueued = i; visibleIn = t; })
                    .Completes();
                runner.MockDispatcher
                    .Setup(d => d.Dispatch(It.IsAny<InvocationContext>()))
                    .Completes(new InvocationResult(result, result == ExecutionResult.Faulted ? new Exception() : null, TimeSpan.FromDays(365)))
                    .Verifiable();

                // Act
                await runner.Dispatch(request, cts.Token);

                // Assert
                Assert.NotNull(enqueued);
                Assert.NotEqual(invocation.Id, enqueued.Id);
                Assert.Equal(invocation.Job, enqueued.Job);
                Assert.Equal(Constants.Source_RepeatingJob, enqueued.Source);
                Assert.Same(invocation.Payload, enqueued.Payload);
                Assert.False(enqueued.Continuation);
                Assert.Equal(runner.VirtualClock.UtcNow + TimeSpan.FromDays(365), enqueued.EstimatedNextVisibleTime.Value);
                Assert.Equal(TimeSpan.FromDays(365), visibleIn.Value);
            }
        }

        internal class TestableJobRunner : JobRunner
        {
            private bool _skipDispatch;

            public Mock<InvocationQueue> MockQueue { get; private set; }
            public Mock<JobDispatcher> MockDispatcher { get; private set; }
            public Mock<StorageHub> MockStorage { get; private set; }
            public VirtualClock VirtualClock { get; private set; }

            public bool CaptureStarted { get; private set; }
            public InvocationRequest LastDispatched { get; private set; }
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
                    .Setup(q => q.Update(It.IsAny<Invocation>()))
                    .Completes();
                MockQueue
                    .Setup(q => q.Acknowledge(It.IsAny<InvocationRequest>()))
                    .Completes();
                MockQueue
                    .Setup(q => q.Enqueue(It.IsAny<Invocation>()))
                    .Completes();
                MockQueue
                    .Setup(q => q.Enqueue(It.IsAny<Invocation>(), It.IsAny<TimeSpan>()))
                    .Completes();
            }

            protected internal override Task Dispatch(InvocationRequest request, CancellationToken cancelToken)
            {
                LastDispatched = request;
                if (_skipDispatch)
                {
                    return DispatchTCS.Task;
                }
                else
                {
                    return base.Dispatch(request, cancelToken);
                }
            }

            protected override Task<InvocationLogCapture> StartCapture(InvocationRequest request)
            {
                CaptureStarted = true;
                return Task.FromResult<InvocationLogCapture>(null);
            }
        }
    }
}
