// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Security;

namespace GalleryTools.Commands
{
    public static class ApplyTenantPolicyCommand
    {
        private const string UserListOption = "--path";
        private const string TenantIdOption = "--tenant";

        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Apply organization tenant policy";
            config.HelpOption("-? | -h | --help");

            var userListOption = config.Option(
                $"-p | {UserListOption}",
                "A path to a list of organizations, one username per line.",
                CommandOptionType.SingleValue);

            var tenantIdOption = config.Option(
                $"-t | {TenantIdOption}",
                "The tenant ID to restrict membership of each organization on the list to.",
                CommandOptionType.SingleValue);

            config.OnExecute(() =>
            {
                return ExecuteAsync(userListOption, tenantIdOption).GetAwaiter().GetResult();
            });
        }

        private static async Task<int> ExecuteAsync(
            CommandOption userListOption,
            CommandOption tenantIdOption)
        {
            if (!userListOption.HasValue())
            {
                Console.WriteLine($"The '{UserListOption}' parameter is required.");
                return 1;
            }

            if (!tenantIdOption.HasValue())
            {
                Console.WriteLine($"The '{TenantIdOption}' parameter is required.");
                return 1;
            }

            var builder = new ContainerBuilder();
            builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
            var container = builder.Build();
            var securityPolicyService = container.Resolve<SecurityPolicyService>();
            var userService = container.Resolve<UserService>();

            var userListPath = userListOption.Value();
            Console.WriteLine($"Reading user list from {userListPath}");
            var usernames = ReadUserList(userListPath);
            Console.WriteLine($"Finished reading user list. Found {usernames.Count()} users");

            var tenantId = tenantIdOption.Value();
            Console.WriteLine($"Restricting organization membership to tenant ID {tenantId}");

            foreach (var username in usernames)
            {
                var user = userService.FindByUsername(username);
                if (user == null)
                {
                    Console.WriteLine($"Could not find user with name {username}... skipping");
                    continue;
                }

                var organization = user as Organization;
                if (organization == null)
                {
                    Console.WriteLine($"User with name {username} is not an organization...skipping");
                    continue;
                }

                Console.WriteLine($"Applying AAD tenant policy to organization with name {username}");
                var tenantPolicy = RequireOrganizationTenantPolicy.Create(tenantId);
                if (await securityPolicyService.SubscribeAsync(organization, tenantPolicy))
                {
                    Console.WriteLine($"Successfully applied AAD tenant policy to organization with name {username}");
                }
                else
                {
                    Console.WriteLine($"Organization with name {username} already has AAD tenant policy.");
                }
            }

            return 0;
        }

        private static IEnumerable<string> ReadUserList(string path)
        {
            var usernames = new HashSet<string>();
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(fileStream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (usernames.Add(line))
                    {
                        // Each username in the list should be distinct, but we should also respect the ordering of the file.
                        yield return line;
                    }
                }
            }
        }
    }
}
