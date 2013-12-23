using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PowerArgs;

namespace NuCmd.Commands
{
    public interface ICommand {
        Task Execute(IConsole console);
    }

    public abstract class Command : ICommand
    {
        [ArgShortcut("!")]
        [ArgShortcut("n")]
        public bool WhatIf { get; set; }

        protected IConsole Console { get; private set; }

        public virtual Task Execute(IConsole console)
        {
            Console = console;
            return OnExecute();
        }

        protected abstract Task OnExecute();
    }
}
