// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Services.Entities;
using NuGetGallery;

namespace GalleryTools.Commands
{
    public static class UpdateIsLatestCommand
    {
        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Update IsLatest(Stable)(SemVer2) properties of all packages in the target NuGetGallery database. !!Please make sure you have created a back-up of your database first!!";
            config.HelpOption("-? | -h | --help");

            var connectionstringOption = config.Option(
                "--connectionstring",
                "The SQL connectionstring of the target NuGetGallery database.",
                CommandOptionType.SingleValue);

            config.OnExecute(async () => await ExecuteAsync(connectionstringOption));
        }

        private static async Task<int> ExecuteAsync(
            CommandOption connectionStringOption)
        {
            if (!connectionStringOption.HasValue())
            {
                Console.Error.WriteLine($"The {connectionStringOption.Template} option is required.");
                return 1;
            }

            Console.Write("Testing database connectivity...");

            using (var sqlConnection = new SqlConnection(connectionStringOption.Value()))
            {
                try
                {
                    await sqlConnection.OpenAsync();
                    Console.WriteLine(" OK");
                }
                catch (Exception exception)
                {
                    Console.WriteLine(" Failed");
                    Console.Error.WriteLine(exception.Message);
                    return -1;
                }

                using (var entitiesContext = new EntitiesContext(sqlConnection, readOnly: false))
                {
                    var packageRegistrationRepository = new EntityRepository<PackageRegistration>(entitiesContext);

                    var packageService = new CorePackageService(
                        new EntityRepository<Package>(entitiesContext),
                        packageRegistrationRepository,
                        new EntityRepository<Certificate>(entitiesContext));

                    Console.WriteLine("Retrieving package registrations...");
                    var packageRegistrations = packageRegistrationRepository.GetAll().ToList();
                    Console.WriteLine($"Processing {packageRegistrations.Count} records...");

                    double counter = 0;
                    foreach (var packageRegistration in packageRegistrations.OrderBy(pr => pr.Id))
                    {
                        var pct = 100 * counter++ / packageRegistrations.Count;

                        Console.Write($"  [{pct.ToString("N2")} %] Updating {packageRegistration.Id} ...");
                        await packageService.UpdateIsLatestAsync(packageRegistration, commitChanges: true);
                        Console.WriteLine(" OK");
                    }
                }
            }

            Console.WriteLine("DONE");
            return 0;
        }
    }
}
