using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PowerArgs;

namespace NuCmd.Commands
{
    public interface ICommand {
        Task Execute(IConsole console, CommandDirectory directory);
    }

    public abstract class Command : ICommand
    {
        [ArgShortcut("!")]
        [ArgShortcut("n")]
        [ArgDescription("Report what the command would do but do not actually perform any changes")]
        public bool WhatIf { get; set; }

        protected IConsole Console { get; private set; }
        protected CommandDirectory Directory { get; private set; }

        public virtual Task Execute(IConsole console, CommandDirectory directory)
        {
            Console = console;
            Directory = directory;
            return OnExecute();
        }

        protected abstract Task OnExecute();
    }
}
