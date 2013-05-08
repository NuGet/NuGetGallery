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
        void Execute();
    }

    /// <summary>
    /// Provides an interface to an operation that produces a result
    /// </summary>
    public interface IQuery
    {
        object Execute();
    }

    public abstract class Query<TResult> : IQuery
    {
        object IQuery.Execute()
        {
            return Execute();
        }

        public abstract TResult Execute();
    }
}
