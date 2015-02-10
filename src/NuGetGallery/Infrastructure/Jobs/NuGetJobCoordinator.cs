using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery.Infrastructure.Jobs
{
    internal sealed class NuGetJobCoordinator : IJobCoordinator
    {
        public void Dispose()
        {
        }

        public Task GetWork(IJob job)
        {
            Task task;
            try
            {
                task = job.Execute();
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return new Task(() => {}); // returning dummy task so the job scheduler doesn't see errors
            }

            // wrapping the task inside an outer task which will continue with an error handler.
            return CreateWrappingContinuingTask(task, t =>
            {
                if (t.IsCanceled)
                {
                    Debug.WriteLine("Background Task Canceled: " + job.GetType());
                }
                else if (t.IsFaulted)
                {
                    Debug.WriteLine("Background Task Faulted: " + job.GetType());
                    QuietLog.LogHandledException(task.Exception);
                }
                // else happiness
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private static Task CreateWrappingContinuingTask(Task originalTask, Action<Task> continuationTask, TaskContinuationOptions options)
        {
            var continuation = originalTask.ContinueWith(continuationTask, options);

            var wrapper = new Task(() =>
            {
                if (originalTask.Status == TaskStatus.WaitingForActivation || originalTask.Status == TaskStatus.Created)
                {
                    originalTask.Start();
                }
                continuation.Wait();
            });

            return wrapper;
        }
    }
}
