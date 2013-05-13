using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Commands
{
    /// <summary>
    /// Provides a base class for an operation that does not produce a result
    /// </summary>
    public abstract class Command
    {
        public abstract void Execute();
    }

    /// <summary>
    /// Provides a base class for an operation that produces a result
    /// </summary>
    public abstract class Command<TResult>
    {
        public abstract TResult Execute();
    }
}
