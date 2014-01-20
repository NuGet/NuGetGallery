using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq.Language;
using Moq.Language.Flow;

namespace Moq
{
    public static class MoqExtensions
    {
        public static TaskCompletionSource<R> WaitsForSignal<T, R>(this IReturns<T, Task<R>> self)
            where T : class
        {
            var tcs = new TaskCompletionSource<R>();
            self.Returns(tcs.Task);
            return tcs;
        }

        public static IReturnsResult<T> Completes<T>(this IReturns<T, Task> self)
            where T : class
        {
            return self.Returns(Task.FromResult<object>(null));
        }

        public static IReturnsResult<T> Completes<T, R>(this IReturns<T, Task<R>> self, R result)
            where T : class
        {
            return self.Returns(Task.FromResult(result));
        }

        public static IReturnsResult<T> Completes<T, R>(this IReturns<T, Task<R>> self, Func<R> result)
            where T : class
        {
            return self.Returns(() => Task.FromResult(result()));
        }
    }
}
