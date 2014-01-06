using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Client;
using PowerArgs;

namespace NuCmd.Commands
{
    public interface ICommand {
        Task Execute(IConsole console, CommandDefinition definition, CommandDirectory directory);
    }

    public abstract class Command : ICommand
    {
        [ArgShortcut("!")]
        [ArgShortcut("n")]
        [ArgDescription("Report what the command would do but do not actually perform any changes")]
        public bool WhatIf { get; set; }

        protected IConsole Console { get; private set; }
        protected CommandDefinition Definition { get; private set; }
        protected CommandDirectory Directory { get; private set; }

        public virtual Task Execute(IConsole console, CommandDefinition definition, CommandDirectory directory)
        {
            Console = console;
            Directory = directory;
            Definition = definition;

            return OnExecute();
        }

        protected abstract Task OnExecute();

        protected virtual async Task<bool> ReportHttpStatus<T>(ServiceResponse<T> response)
        {
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            await Console.WriteErrorLine(
                Strings.Commands_HttpError,
                response.StatusCode,
                response.ReasonPhrase);
            return false;
        }
    }
}
