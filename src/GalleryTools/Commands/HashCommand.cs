// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Authentication;

namespace GalleryTools.Commands
{
    public static class HashCommand
    {
        const int BatchSize = 100;

        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Hash V1/V2 ApiKeys in Gallery DB";
            CommandOption whatIf = config.Option("-w | --whatif", "Don't apply changes to DB.", CommandOptionType.NoValue);
            config.HelpOption("-? | -h | --help");
            config.OnExecute(() =>
            {
                var builder = new ContainerBuilder();
                builder.RegisterType<ReflowPackageService>().AsSelf();
                builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
                var container = builder.Build();
                var connectionString = container.Resolve<IAppConfiguration>().SqlConnectionString;

                Hash(connectionString, whatIf.HasValue()).GetAwaiter().GetResult();

                return 0;
            });
        }

        private static async Task Hash(string connectionString, bool whatIf)
        {
            using (var context = new EntitiesContext(connectionString, readOnly: whatIf))
            {
                var allCredentials = (IQueryable<Credential>)context.Set<Credential>();

                // Get all V1/V2 credentials that are active: 
                // V1 credentials that have no expiration date, but were used in the last year
                // V1/V2 credentials that have a future expiration date

                var validLastUsed = DateTime.UtcNow - TimeSpan.FromDays(365);
                Expression<Func<Credential, bool>> predicate = x => (x.Type == CredentialTypes.ApiKey.V1 || x.Type == CredentialTypes.ApiKey.V2) &&
                                                                ((x.Expires == null && x.LastUsed != null && x.LastUsed > validLastUsed) ||
                                                                (x.Expires != null && x.Expires > DateTime.UtcNow));

                var activeCredentialsCount = allCredentials.Count(predicate);

                Console.WriteLine($"Found {activeCredentialsCount} active V1/V2 ApiKeys.");

                IList<Credential> batch;
                int batchNumber = 1;
                bool failures = false;

                do
                {
                    batch = allCredentials.Where(predicate).OrderBy(x => x.Key).Take(BatchSize).ToList();

                    if (batch.Count > 0)
                    {
                        Console.WriteLine($"Hashing batch {batchNumber}/{Math.Ceiling((double)activeCredentialsCount / (double)BatchSize)}. Batch size: {batch.Count}...");

                        foreach (var v1v2ApiKey in batch)
                        {
                            ApiKeyV3 hashedApiKey = null;

                            try
                            {
                                hashedApiKey = ApiKeyV3.CreateFromV1V2ApiKey(v1v2ApiKey.Value);

                                v1v2ApiKey.Type = CredentialTypes.ApiKey.V3;
                                v1v2ApiKey.Value = hashedApiKey.HashedApiKey;
                            }
                            catch (ArgumentException e)
                            {
                                failures = true;
                                Console.WriteLine($"Failed to hash key: {v1v2ApiKey.Key} Type: {v1v2ApiKey.Type} Old value: {v1v2ApiKey.Value}. Reason: {e}");
                            }
                        }

                        if (!failures && !whatIf)
                        {
                            var stopwatch = Stopwatch.StartNew();
                            Console.WriteLine($"Saving batch {batchNumber} to DB...");
                            await context.SaveChangesAsync();
                            Console.WriteLine($"Saved changes to DB. Took: {stopwatch.Elapsed.TotalSeconds} seconds");
                        }
                        else
                        {
                            Console.WriteLine("Skipping DB save.");
                            allCredentials = allCredentials.Where(predicate).OrderBy(x => x.Key).Skip(batch.Count);
                        }

                        batchNumber++;
                    }
                }
                while (batch.Count > 0);
            }
        }
    }
}
