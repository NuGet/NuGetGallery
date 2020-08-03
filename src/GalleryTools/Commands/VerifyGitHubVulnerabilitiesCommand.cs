// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.Collector;
using GitHubVulnerabilities2Db.Configuration;
using GitHubVulnerabilities2Db.GraphQL;
using GitHubVulnerabilities2Db.Ingest;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;

namespace GalleryTools.Commands
{
    /// <summary>
    /// This command verifies that the <see cref="PackageVulnerability"/> and <see cref="VulnerablePackageVersionRange"/> entities in the 
    /// database match the <see cref="SecurityAdvisory"/> and <see cref="SecurityVulnerability"/> entities in GitHub's V4 GraphQL API.
    /// </summary>
    /// <remarks>
    /// The verification only expects that advisories that are present in the GitHub API have the same metadata and contain the same ranges in the DB.
    /// It intentionally does not require that all vulnerabilities in the DB come from GitHub, or that the set of ranges in the DB match the set of ranges in the GitHub API.
    /// This is so that we can add some additional vulnerabilities or ranges for testing or administrative purposes.
    /// </remarks>
    public static class VerifyGitHubVulnerabilitiesCommand
    {
        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Verify that the gallery database's vulnerability information matches GitHub's feed.";
            config.HelpOption("-? | -h | --help");

            var gitHubPersonalAccessTokenOption = config.Option(
                "--token | -t",
                "The personal access token to use to authenticate with GitHub.",
                CommandOptionType.SingleValue);

            var connectionStringOption = config.Option(
                "--connectionstring | -c",
                "The SQL connectionstring of the target NuGetGallery database.",
                CommandOptionType.SingleValue);

            config.OnExecute(async () => await ExecuteAsync(
                connectionStringOption,
                gitHubPersonalAccessTokenOption));
        }

        private static async Task<int> ExecuteAsync(
            CommandOption connectionStringOption,
            CommandOption gitHubPersonalAccessTokenOption)
        {
            if (!connectionStringOption.HasValue())
            {
                Console.Error.WriteLine($"The {connectionStringOption.Template} option is required.");
                return 1;
            }

            if (!gitHubPersonalAccessTokenOption.HasValue())
            {
                Console.Error.WriteLine($"The {connectionStringOption.Template} option is required.");
                return 1;
            }

            try
            {
                var advisories = await FetchAdvisories(gitHubPersonalAccessTokenOption.Value());
                await VerifyPackageVulnerabilities(
                    connectionStringOption.Value(),
                    advisories);

                Console.WriteLine("DONE");
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(" FAILED");
                Console.Error.WriteLine(e.Message);
                return 1;
            }
        }

        private static async Task<IReadOnlyList<SecurityAdvisory>> FetchAdvisories(
            string token)
        {
            Console.Write("Fetching vulnerabilities from GitHub...");

            var config = new GitHubVulnerabilities2DbConfiguration
            {
                GitHubPersonalAccessToken = token
            };

            var queryService = new QueryService(
                config,
                new HttpClient());

            var advisoryQueryService = new AdvisoryQueryService(
                queryService,
                new AdvisoryQueryBuilder(),
                NullLogger<AdvisoryQueryService>.Instance);

            var advisories = await advisoryQueryService.GetAdvisoriesSinceAsync(DateTimeOffset.MinValue, CancellationToken.None);
            Console.WriteLine($" FOUND {advisories.Count} advisories.");
            return advisories;
        }

        private static async Task VerifyPackageVulnerabilities(
            string connectionString,
            IReadOnlyList<SecurityAdvisory> advisories)
        {
            Console.WriteLine("Fetching vulnerabilities from DB...");

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                await sqlConnection.OpenAsync();
                using (var entitiesContext = new EntitiesContext(sqlConnection, readOnly: false))
                {
                    var verifier = new PackageVulnerabilityServiceVerifier(entitiesContext);
                    var ingestor = new AdvisoryIngestor(verifier, new GitHubVersionRangeParser());
                    await ingestor.IngestAsync(advisories);

                    if (verifier.HasErrors)
                    {
                        throw new Exception("DB does not match GitHub API!");
                    }

                    Console.WriteLine("DB matches GitHub API!");
                }
            }
        }

        public class PackageVulnerabilityServiceVerifier : IPackageVulnerabilityService
        {
            private readonly IEntitiesContext _entitiesContext;

            public PackageVulnerabilityServiceVerifier(
                IEntitiesContext entitiesContext)
            {
                _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            }

            public void ApplyExistingVulnerabilitiesToPackage(Package package)
            {
                throw new NotImplementedException();
            }

            public Task UpdateVulnerabilityAsync(PackageVulnerability vulnerability, bool withdrawn)
            {
                Console.WriteLine($"Verifying vulnerability {vulnerability.GitHubDatabaseKey}.");
                var existingVulnerability = _entitiesContext.Vulnerabilities
                    .Include(v => v.AffectedRanges)
                    .SingleOrDefault(v => v.GitHubDatabaseKey == vulnerability.GitHubDatabaseKey);

                if (withdrawn || !vulnerability.AffectedRanges.Any())
                {
                    if (existingVulnerability != null)
                    {
                        Console.Error.WriteLine(withdrawn ? 
                            $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey} was withdrawn and should not be in DB!" :
                            $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey} affects no packages and should not be in DB!");
                        HasErrors = true;
                    }

                    return Task.CompletedTask;
                }

                if (existingVulnerability == null)
                {
                    Console.Error.WriteLine($"Cannot find vulnerability {vulnerability.GitHubDatabaseKey} in DB!");
                    HasErrors = true;
                    return Task.CompletedTask;
                }

                if (existingVulnerability.Severity != vulnerability.Severity)
                {
                    Console.Error.WriteLine(
                        $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                        }, severity does not match! GitHub: {vulnerability.Severity}, DB: {existingVulnerability.Severity}");
                    HasErrors = true;
                }

                if (existingVulnerability.AdvisoryUrl != vulnerability.AdvisoryUrl)
                {
                    Console.Error.WriteLine(
                        $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                        }, advisory URL does not match! GitHub: {vulnerability.AdvisoryUrl}, DB: { existingVulnerability.AdvisoryUrl}");
                    HasErrors = true;
                }

                foreach (var range in vulnerability.AffectedRanges)
                {
                    Console.WriteLine($"Verifying range affecting {range.PackageId} {range.PackageVersionRange}.");
                    var existingRange = existingVulnerability.AffectedRanges
                        .SingleOrDefault(r => r.PackageId == range.PackageId && r.PackageVersionRange == range.PackageVersionRange);

                    if (existingRange == null)
                    {
                        Console.Error.WriteLine(
                            $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                            }, cannot find range {range.PackageId} {range.PackageVersionRange} in DB!");
                        HasErrors = true;
                        continue;
                    }

                    if (existingRange.FirstPatchedPackageVersion != range.FirstPatchedPackageVersion)
                    {
                        Console.Error.WriteLine(
                            $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                            }, range {range.PackageId} {range.PackageVersionRange}, first patched version does not match! GitHub: {
                            range.FirstPatchedPackageVersion}, DB: {range.FirstPatchedPackageVersion}");
                        HasErrors = true;
                    }

                    var packages = _entitiesContext.Packages
                        .Where(p => p.PackageRegistration.Id == range.PackageId)
                        .Include(p => p.Vulnerabilities)
                        .ToList();

                    var versionRange = VersionRange.Parse(range.PackageVersionRange);
                    foreach (var package in packages)
                    {
                        var version = NuGetVersion.Parse(package.NormalizedVersion);
                        if (versionRange.Satisfies(version) != package.Vulnerabilities.Contains(existingRange))
                        {
                            Console.Error.WriteLine(
                                $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                                }, range {range.PackageId} {range.PackageVersionRange}, package {package.NormalizedVersion
                                } is not properly marked vulnerable to vulnerability!");
                            HasErrors = true;
                        }
                    }
                }

                return Task.CompletedTask;
            }

            public bool HasErrors { get; private set; }
        }
    }
}
