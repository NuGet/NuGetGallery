using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Commands
{
    /// <summary>
    /// Provides a base class for an operation that produces a result
    /// </summary>
    public abstract class Command<TResult>
    {
        public override bool Equals(object obj)
        {
            // Equal if the objects are the same type
            // Subclasses should check parameters
            return Equals(obj.GetType(), GetType());
        }

        // Compiler complains if we override Equals but not this.
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    internal interface ICommandHandler
    {
        object Execute(object command);
    }

    public abstract class CommandHandler<TCommand, TResult> : ICommandHandler where TCommand : Command<TResult>
    {
        public abstract TResult Execute(TCommand command);

        object ICommandHandler.Execute(object command)
        {
            return Execute((TCommand)command);
        }
    }
}
