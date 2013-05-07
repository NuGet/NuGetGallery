using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Commands
{
    /// <summary>
    /// Provides an interface to an operation that does not produce a result
    /// </summary>
    public interface ICommand
    {
        Task Execute();
    }

    /// <summary>
    /// Provides an interface to an operation that produces a result
    /// </summary>
    public interface IQuery
    {
        Task<object> Execute();
    }

    public abstract class Query<TResult> : IQuery
    {
        async Task<object> IQuery.Execute()
        {
            return await Execute();
        }

        protected abstract Task<TResult> Execute();
    }
}
