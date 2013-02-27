using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public static class TaskExtensions
    {
        public async static Task<T> TimeoutAfter<T>(this Task<T> self, int timeoutInMilliseconds)
        {
            await TaskEx.WhenAny(self, TaskEx.Delay(timeoutInMilliseconds));
            if (!self.IsCompleted)
            {
                throw new TimeoutException();
            }
            else
            {
                return self.Result;
            }
        }
    }
}
