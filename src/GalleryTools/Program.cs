// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System;
using System.Threading.Tasks;
using NuGetGallery;
using NuGetGallery.Infrastructure.Authentication;
using Microsoft.Extensions.CommandLineUtils;

namespace GalleryTools
{
    class Program
    {
        public static void Main(params string[] args)
        {
            var commandLineApplication = new CommandLineApplication();
            commandLineApplication.HelpOption("-? | -h | --help");

            var hash = commandLineApplication.Command("hash", config =>
            {
                config.Description = "Hash V1/V2 ApiKeys in Gallery DB";
                CommandOption connectionString = config.Option("-db", "DB connection string. User should have write permission.", CommandOptionType.SingleValue);
                CommandOption whatIf = config.Option("-w | --whatif", "Don't apply changes to DB.", CommandOptionType.NoValue);
                config.HelpOption("-? | -h | --help");
                config.OnExecute(() =>
                {
                    Hash(connectionString.Value(), whatIf.HasValue()).Wait();
                    return 0;
                });
            });

            commandLineApplication.Execute(args);
        }

        private static async Task Hash(string connectionString, bool whatIf)
        {
            IEntitiesContext context = new EntitiesContext(connectionString, readOnly: whatIf);
            var allCredentials = (IQueryable<Credential>)context.Set<Credential>();

            // Get all V1/V2 credentials that are active: 
            // V1 credentials that have no expiration date, but were used in the last year
            // V1/V2 credentials that have a future expiration date

            var validLastUsed = DateTime.UtcNow - TimeSpan.FromDays(365);
            var activeCredentials = 
                allCredentials.Where(x => (x.Type == CredentialTypes.ApiKey.V1 || x.Type == CredentialTypes.ApiKey.V2) &&
                                          ((x.Expires == null && x.LastUsed != null && x.LastUsed > validLastUsed) ||
                                           (x.Expires != null && x.Expires > DateTime.UtcNow)))
                                          .ToList();

            Console.WriteLine($"Found {activeCredentials.Count} active V1/V2 ApiKeys. Ids: " +
                                     $"{string.Join(", ", activeCredentials.Select(x => x.Key))}");

            bool failures = false;

            foreach (var v1v2ApiKey in activeCredentials)
            {
                ApiKeyV3 hashedApiKey = null;

                try
                {
                    hashedApiKey = ApiKeyV3.CreateFromV1V2ApiKey(v1v2ApiKey.Value);
                    
                    Console.WriteLine($"Key: {v1v2ApiKey.Key} Type: {v1v2ApiKey.Type} Old value: {v1v2ApiKey.Value} New value: {hashedApiKey.HashedApiKey}");

                    v1v2ApiKey.Type = CredentialTypes.ApiKey.V3;
                    v1v2ApiKey.Value = hashedApiKey.HashedApiKey;
                }
                catch (ArgumentException e)
                {
                    failures = true;
                    Console.WriteLine($"Failed to hash key: {v1v2ApiKey.Key} Type: {v1v2ApiKey.Type} Old value: {v1v2ApiKey.Value}. Reason: {e.Message}");
                }
            }

            if (activeCredentials.Count > 0 && !failures && !whatIf)
            {
                await context.SaveChangesAsync();
                Console.WriteLine("Saved changes to DB.");
            }
        }
    }
}
