using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
        protected NuGetEnvironment TargetEnvironment { get; set; }

        public virtual async Task Execute(IConsole console, CommandDefinition definition, CommandDirectory directory)
        {
            if (await LoadContext(console, definition, directory))
            {
                await OnExecute();
            }
        }

        protected virtual Task<bool> LoadContext(IConsole console, CommandDefinition definition, CommandDirectory directory)
        {
            Console = console;
            Directory = directory;
            Definition = definition;

            // Load the environment
            TargetEnvironment = LoadEnvironment();

            return Task.FromResult(true);
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
                (int)response.StatusCode,
                response.ReasonPhrase,
                await response.HttpResponse.Content.ReadAsStringAsync());
            return false;
        }

        protected virtual NuGetEnvironment LoadEnvironment()
        {
            string name = Environment.GetEnvironmentVariable("NUCMD_ENVIRONMENT");
            if(String.IsNullOrEmpty(name)) 
            {
                return null;
            }
            var env = new NuGetEnvironment()
            {
                Name = name,
                SubscriptionId = Environment.GetEnvironmentVariable("NUCMD_SUBSCRIPTION_ID"),
                SubscriptionName = Environment.GetEnvironmentVariable("NUCMD_SUBSCRIPTION_NAME")
            };

            // Load the cert
            string thumbprint = Environment.GetEnvironmentVariable("NUCMD_SUBSCRIPTION_THUMBPRINT");
            if(!String.IsNullOrEmpty(thumbprint)) {
                X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                env.SubscriptionCertificate = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    thumbprint,
                    validOnly: false)
                    .Cast<X509Certificate2>()
                    .FirstOrDefault();
            }

            // Load the service map
            var servicesString = Environment.GetEnvironmentVariable("NUCMD_SERVICE_MAP");
            if(!String.IsNullOrEmpty(servicesString)) {
                foreach(var service in servicesString.Split(';')) {
                    var vals = service.Split('|');
                    if(vals.Length == 2) {
                        env.ServiceMap.Add(vals[0], new Uri(vals[1], UriKind.Absolute));
                    }
                }
            }
            return env;
        }
    }
}
