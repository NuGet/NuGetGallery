// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;

namespace GalleryTools.Commands
{
    public static class SeedFunctionalTestsCommand
    {
        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Seed users, organizations, API keys, and test packages for functional tests";
            config.HelpOption("-? | -h | --help");

            var outputOption = config.Option(
                "-o | --output", "Path to write settings.CI.json (required).",
                CommandOptionType.SingleValue);

            var packageDirOption = config.Option(
                "-p | --package-dir", "Directory containing .nupkg.testdata files to push (required).",
                CommandOptionType.SingleValue);

            var baseUrlOption = config.Option(
                "--base-url", "Gallery base URL for test config (default: https://localhost).",
                CommandOptionType.SingleValue);

            config.OnExecute(() =>
            {
                if (!outputOption.HasValue() || !packageDirOption.HasValue())
                {
                    Console.WriteLine("--output and --package-dir are required.");
                    config.ShowHelp();
                    return 1;
                }

                var baseUrl = baseUrlOption.HasValue() ? baseUrlOption.Value() : "https://localhost";
                return ExecuteAsync(outputOption.Value(), packageDirOption.Value(), baseUrl).GetAwaiter().GetResult();
            });
        }

        private static async Task<int> ExecuteAsync(string outputPath, string packageDir, string baseUrl)
        {
            if (!Directory.Exists(packageDir))
            {
                Console.Error.WriteLine($"Package directory not found: {packageDir}");
                return 1;
            }

            var nupkgFiles = Directory.GetFiles(packageDir, "*.nupkg.testdata");
            if (nupkgFiles.Length == 0)
            {
                Console.Error.WriteLine($"No .nupkg.testdata files found in: {packageDir}");
                return 1;
            }

            var builder = new ContainerBuilder();
            builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
            var container = builder.Build();

            var context = container.Resolve<IEntitiesContext>();
            var ops = new GalleryOperations(context, container.Resolve<ICredentialBuilder>());

            // ─── Test account names ──────────────────────────────────────────────────
            const string testUser = "NugetTestAccount";
            const string testPassword = "Password1!";
            const string testEmail = "testnuget@localhost";

            const string orgAdminUser = "NugetOrgAdmin";
            const string orgAdminPassword = "Password1!";

            const string adminOrgName = "NugetTestAdminOrganization";
            const string collaboratorOrgName = "NugetTestCollaboratorOrganization";
            const string testDataOrgName = "NuGetTestData";

            // ─── 1. Create users ─────────────────────────────────────────────────────
            var testAccount = await ops.EnsureUserAsync(testUser, testPassword, testEmail);
            var orgAdmin = await ops.EnsureUserAsync(orgAdminUser, orgAdminPassword, $"{orgAdminUser}@localhost");

            // ─── 2. Create organizations ─────────────────────────────────────────────
            await ops.EnsureOrganizationAsync(testDataOrgName, admin: testAccount, collaborator: null);
            await ops.EnsureOrganizationAsync(adminOrgName, admin: testAccount, collaborator: null);
            await ops.EnsureOrganizationAsync(collaboratorOrgName, admin: orgAdmin, collaborator: testAccount);

            // ─── 3. Create API keys ──────────────────────────────────────────────────
            var testDataOrgEntity = context.Users.First(u => u.Username == testDataOrgName);
            var accountApiKey = ops.CreateApiKey(testAccount, "CI Full Access", scopeActions: new[] { NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion, NuGetScopes.PackageUnlist }, scopeOwner: testAccount);
            var apiKeyPush = ops.CreateApiKey(testAccount, "CI Push", scopeActions: new[] { NuGetScopes.PackagePush }, scopeOwner: testAccount);
            var apiKeyPushVersion = ops.CreateApiKey(testAccount, "CI Push Version", scopeActions: new[] { NuGetScopes.PackagePushVersion }, scopeOwner: testAccount);
            var apiKeyUnlist = ops.CreateApiKey(testAccount, "CI Unlist", scopeActions: new[] { NuGetScopes.PackageUnlist }, scopeOwner: testAccount);

            // Org API keys: credential on testAccount, scoped to the org
            var testDataOrgApiKey = ops.CreateApiKey(testAccount, "CI TestData Org Push", scopeActions: new[] { NuGetScopes.PackagePush }, scopeOwner: testDataOrgEntity);
            var adminOrgEntity = context.Users.First(u => u.Username == adminOrgName);
            var collabOrgEntity = context.Users.First(u => u.Username == collaboratorOrgName);

            var adminOrgApiKey = ops.CreateApiKey(testAccount, "CI Admin Org", scopeActions: null, scopeOwner: adminOrgEntity);
            var collabOrgApiKey = ops.CreateApiKey(testAccount, "CI Collaborator Org", scopeActions: null, scopeOwner: collabOrgEntity);

            await context.SaveChangesAsync();
            Console.WriteLine("All API keys created.");

            // ─── 4. Push test packages via Gallery API ──────────────────────────────
            // Push through the gallery's HTTP API so the in-process Lucene index
            // is updated automatically by ApiController.UpdatePackage().
            // Use the org-scoped key so packages are owned by NuGetTestData org.
            // Exception: the locked package is pushed with the user-scoped key so the
            // LockedPackageCannotBeModified test can attempt to push with Account.ApiKey.
            const string lockedPackageFile = "nugettest_lockedpackagecannotbemodified.1.0.0.nupkg.testdata";
            foreach (var nupkgPath in nupkgFiles)
            {
                var fileName = Path.GetFileName(nupkgPath).ToLowerInvariant();
                var key = fileName == lockedPackageFile ? apiKeyPush : testDataOrgApiKey;
                await GalleryOperations.PushPackageAsync(baseUrl, key, nupkgPath);
            }

            // ─── 5. Lock the locked-package test fixture ─────────────────────────────
            const string lockedPackageId = "NuGetTest_LockedPackageCannotBeModified";
            var lockedReg = context.PackageRegistrations.FirstOrDefault(r => r.Id == lockedPackageId);
            if (lockedReg != null)
            {
                if (!lockedReg.IsLocked)
                {
                    lockedReg.IsLocked = true;
                    await context.SaveChangesAsync();
                    Console.WriteLine($"Locked package registration '{lockedPackageId}'.");
                }
                else
                {
                    Console.WriteLine($"Package registration '{lockedPackageId}' already locked.");
                }
            }
            else
            {
                Console.Error.WriteLine($"WARNING: Package '{lockedPackageId}' not found — LockedPackageCannotBeModified test will fail.");
            }

            // ─── 6. Write settings.CI.json ───────────────────────────────────────────
            var settings = new JObject(
                new JProperty("DefaultSecurityPoliciesEnforced", true),
                new JProperty("TestPackageLock", true),
                new JProperty("TyposquattingCheckAndBlockUsers", false),
                new JProperty("HasSearchService", false),
                new JProperty("HasStatisticsService", false),
                new JProperty("HasManyVersions", false),
                new JProperty("Account", new JObject(
                    new JProperty("Name", testUser),
                    new JProperty("Email", testEmail),
                    new JProperty("Password", testPassword),
                    new JProperty("ApiKey", accountApiKey),
                    new JProperty("ApiKeyPush", apiKeyPush),
                    new JProperty("ApiKeyPushVersion", apiKeyPushVersion),
                    new JProperty("ApiKeyUnlist", apiKeyUnlist))),
                new JProperty("AdminOrganization", new JObject(
                    new JProperty("Name", adminOrgName),
                    new JProperty("ApiKey", adminOrgApiKey))),
                new JProperty("CollaboratorOrganization", new JObject(
                    new JProperty("Name", collaboratorOrgName),
                    new JProperty("ApiKey", collabOrgApiKey))),
                new JProperty("ProductionBaseUrl", baseUrl));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, settings.ToString(Formatting.Indented));
            Console.WriteLine($"Settings written to: {outputPath}");

            return 0;
        }

    }
}
