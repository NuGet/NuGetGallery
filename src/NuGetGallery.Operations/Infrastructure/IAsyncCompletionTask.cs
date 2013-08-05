using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Infrastructure
{
    /// <summary>
    /// Provides an interface to a task which kicks off an asynchronous job to be checked on later
    /// </summary>
    public interface IAsyncCompletionTask
    {
        TimeSpan MaximumPollingLength { get; }
        TimeSpan RecommendedPollingPeriod { get; }
        bool PollForCompletion();
    }
}
