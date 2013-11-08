using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Monitoring
{
    public interface IAsyncDeferred<T>
    {
        Task Complete(T result);
    }

    public class AsyncDeferred<T> : IAsyncDeferred<T>
    {
        private Func<T, Task> _action;

        public AsyncDeferred(Func<T, Task> action)
        {
            _action = action;
        }

        public Task Complete(T result)
        {
            return _action(result);
        }
    }
}
