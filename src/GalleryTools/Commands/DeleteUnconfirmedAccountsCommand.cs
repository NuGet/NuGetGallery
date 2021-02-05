// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Auditing;

namespace GalleryTools.Commands
{
    public static class DeleteUnconfirmedAccountsCommand
    {
        private const int DegreeOfParallelism = 64;
        private const int MaxRetries = 3;

        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Delete unconfirmed accounts";

            config.OnExecute(() =>
            {
                return ExecuteAsync().GetAwaiter().GetResult();
            });
        }

        private static async Task<int> ExecuteAsync()
        {
            var builder = new ContainerBuilder();
            builder
                .RegisterAssemblyModules(
                    typeof(DefaultDependenciesModule).Assembly);

            builder
                .RegisterType<NullAuditingService>()
                .As<IAuditingService>();

            var container = builder.Build();

            ConcurrentBag<string> usernameBag;
            using (var scope = container.BeginLifetimeScope())
            {
                var userRepository = scope.Resolve<IEntityRepository<User>>();

                var usernames = userRepository
                    .GetAll()
                    .Where(u => u.EmailAddress == null && !u.IsDeleted)
                    .Select(u => u.Username)
                    .ToList();

                usernameBag = new ConcurrentBag<string>(usernames);
            }

            var tasks = Enumerable
                .Repeat(0, DegreeOfParallelism)
                .Select(i => DeleteAccounts(container, usernameBag));

            var failures = (await Task.WhenAll(tasks)).SelectMany(u => u).ToList();
            foreach (var failure in failures)
            {
                Console.WriteLine($"Failed to delete {failure}");
            }

            return 0;
        }

        private static async Task<List<string>> DeleteAccounts(IContainer container, ConcurrentBag<string> usernameBag)
        {
            var failures = new List<string>();
            using (var scope = container.BeginLifetimeScope())
            {
                var userRepository = scope.Resolve<IEntityRepository<User>>();
                var deleteAccountService = scope.Resolve<IDeleteAccountService>();

                while (usernameBag.TryTake(out var username))
                {
                    var result = false;
                    var tries = 0;
                    while (true)
                    {
                        result = await DeleteAccount(userRepository, deleteAccountService, username);
                        if (!result && tries < MaxRetries)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(tries));
                            tries++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (!result)
                    {
                        failures.Add(username);
                    }
                }
            }

            return failures;
        }

        private static async Task<bool> DeleteAccount(IEntityRepository<User> userRepository, IDeleteAccountService deleteAccountService, string username)
        {
            Console.WriteLine($"Attempting to delete {username}");
            try
            {
                var user = userRepository.GetAll().Single(u => u.Username == username);
                var result = await deleteAccountService.DeleteAccountAsync(
                    user, user, AccountDeletionOrphanPackagePolicy.KeepOrphans);
                if (!result.Success)
                {
                    throw new Exception(result.Description);
                }

                Console.WriteLine($"Successfully deleted {username}");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to delete {username}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Some invalid usernames will fail when writing to the file system, so don't try to write audit logs.
        /// </summary>
        private class NullAuditingService : AuditingService
        {
            protected override Task SaveAuditRecordAsync(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
            {
                return Task.FromResult(0);
            }
        }
    }
}
