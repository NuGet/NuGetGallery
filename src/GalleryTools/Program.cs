// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using GalleryTools.Commands;
using GalleryTools.Utils;
using Microsoft.Extensions.CommandLineUtils;

namespace GalleryTools
{
    class Program
    {
        public static int Main(params string[] args)
        {
            var commandLineApplication = new CommandLineApplication();

            commandLineApplication.HelpOption("-? | -h | --help");
            commandLineApplication.Command("hash", HashCommand.Configure);
            commandLineApplication.Command("reflow", ReflowCommand.Configure);
            commandLineApplication.Command("applyTenantPolicy", ApplyTenantPolicyCommand.Configure);
            commandLineApplication.Command("fillrepodata", BackfillRepositoryMetadataCommand.Configure);
            commandLineApplication.Command("filldevdeps", BackfillDevelopmentDependencyCommand.Configure);
            commandLineApplication.Command("verifyapikey", VerifyApiKeyCommand.Configure);
            commandLineApplication.Command("updateIsLatest", UpdateIsLatestCommand.Configure);
            commandLineApplication.Command("deleteUnconfirmed", DeleteUnconfirmedAccountsCommand.Configure);

            ConfigureActiveDirectoryInteractiveSqlAuthentication(commandLineApplication);

            try
            {
                return commandLineApplication.Execute(args);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e}");
                return 1;
            }
        }

        private static void ConfigureActiveDirectoryInteractiveSqlAuthentication(CommandLineApplication commandLineApplication)
        {
            foreach (var command in commandLineApplication.Commands)
            {
                var clientIdOption = command.Option(
                    "-c | --clientId",
                    "The client ID to use to authenticate over interactive AAD.", CommandOptionType.SingleValue);

                var interactiveAuthProvider = new InteractiveActiveDirectoryAuthProvider();
                SqlAuthenticationProvider.SetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryInteractive,
                    interactiveAuthProvider);

                // We want to set the client ID before the existing command, but not replace it.
                var existingCommandInvocation = command.Invoke;
                command.OnExecute(
                    () =>
                    {
                        if (clientIdOption.HasValue())
                        {
                            interactiveAuthProvider.ClientId = clientIdOption.Value();
                        }

                        return existingCommandInvocation();
                    });
            }
        }
    }
}
