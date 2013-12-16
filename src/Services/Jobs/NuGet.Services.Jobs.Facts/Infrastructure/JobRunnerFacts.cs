using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using NuGet.Services.Configuration;
using NuGet.Services.Storage;
using Xunit;

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
                    .ReturnsTCS();
                
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
                    .ReturnsTCS();
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
                    .ReturnsTCS();
                var request = new InvocationRequest(new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string,string>()), new CloudQueueMessage("foo"));
                
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
                    .ReturnsTCS();
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
                    .ReturnsTCS();
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
                var invocation = new Invocation(Guid.NewGuid(), "test", "test", new Dictionary<string,string>());
                var request = new InvocationRequest(invocation, new CloudQueueMessage("foo"));
                runner.MockQueue
                    .Setup(q => q.Update(invocation))
                    .Completes()
                    .Verifiable();

                // Pre-cancel the CTS so that the Dispatch method short-cuts the other junk
                cts.Cancel();

                // Act
                await runner.Dispatch(request, cts.Token);

                // Assert
                Assert.Equal(runner.VirtualClock.UtcNow, request.Invocation.LastDequeuedAt.Value);
                Assert.Equal(InvocationStatus.Executing, request.Invocation.Status);
                runner.MockQueue.VerifyAll();
            }
        }

        internal class TestableJobRunner : JobRunner
        {
            private bool _skipDispatch;
            public Mock<InvocationQueue> MockQueue { get; private set; }
            public Mock<JobDispatcher> MockDispatcher { get; private set; }
            public Mock<StorageHub> MockStorage { get; private set; }
            public VirtualClock VirtualClock { get; private set; }

            public InvocationRequest LastDispatched { get; private set; }
            public TaskCompletionSource<object> DispatchTCS { get; set; }
            
            public TestableJobRunner(TimeSpan pollInterval, bool skipDispatch = false) : base(pollInterval)
            {
                // Arrange
                Queue = (MockQueue = new Mock<InvocationQueue>()).Object;
                Dispatcher = (MockDispatcher = new Mock<JobDispatcher>()).Object;
                Storage = (MockStorage = new Mock<StorageHub>()).Object;
                Clock = VirtualClock = new VirtualClock();

                _skipDispatch = skipDispatch;
                DispatchTCS = new TaskCompletionSource<object>();
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
        }
    }
}
