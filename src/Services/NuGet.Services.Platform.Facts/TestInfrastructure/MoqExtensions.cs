using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq.Language.Flow;

namespace Moq
{
    public static class MoqExtensions
    {
        public static TaskCompletionSource<R> ReturnsTCS<T, R>(this ISetup<T, Task<R>> self)
            where T : class
        {
            var tcs = new TaskCompletionSource<R>();
            self.Returns(tcs.Task);
            return tcs;
        }

        public static IReturnsResult<T> Completes<T>(this ISetup<T, Task> self)
            where T : class
        {
            return self.Returns(Task.FromResult<object>(null));
        }

        public static IReturnsResult<T> Completes<T, R>(this ISetup<T, Task<R>> self, R result)
            where T : class
        {
            return self.Returns(Task.FromResult(result));
        }
    }
}
