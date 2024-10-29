// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using GalleryTools.Commands;
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
            commandLineApplication.Command("filltfms", BackfillTfmMetadataCommand.Configure);
            commandLineApplication.Command("verifyapikey", VerifyApiKeyCommand.Configure);
            commandLineApplication.Command("updateIsLatest", UpdateIsLatestCommand.Configure);
            commandLineApplication.Command("reservenamespaces", ReserveNamespacesCommand.Configure);

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
    }
}
