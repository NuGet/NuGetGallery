// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGetGallery;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;

namespace GalleryTools.Commands
{
	/// <summary>
	/// Creates a user, organization, and pushes a single package via the Gallery HTTP API.
	/// Designed for Aspire AppHost bootstrapping so the V3 pipeline has data to process.
	/// </summary>
	public static class SeedSinglePackageCommand
	{
		public static void Configure(CommandLineApplication config)
		{
			config.Description = "Create a user + org and push a single package via the Gallery API";
			config.HelpOption("-? | -h | --help");

			var packageOption = config.Option(
				"-p | --package", "Path to the .nupkg or .nupkg.testdata file (required).",
				CommandOptionType.SingleValue);

			var baseUrlOption = config.Option(
				"--base-url", "Gallery base URL for HTTP push (required).",
				CommandOptionType.SingleValue);

			var usernameOption = config.Option(
				"-u | --username", "Username for the seed account (default: NugetTestAccount).",
				CommandOptionType.SingleValue);

			var passwordOption = config.Option(
				"--password", "Password for the seed account (default: Password1!).",
				CommandOptionType.SingleValue);

			var orgOption = config.Option(
				"-o | --org", "Organization name (default: NuGetTestData).",
				CommandOptionType.SingleValue);

			config.OnExecute(() =>
			{
				if (!packageOption.HasValue() || !baseUrlOption.HasValue())
				{
					Console.WriteLine("--package and --base-url are required.");
					config.ShowHelp();
					return 1;
				}

				var username = usernameOption.HasValue() ? usernameOption.Value() : "NugetTestAccount";
				var password = passwordOption.HasValue() ? passwordOption.Value() : "Password1!";
				var org = orgOption.HasValue() ? orgOption.Value() : "NuGetTestData";

				return ExecuteAsync(
					packageOption.Value(),
					baseUrlOption.Value(),
					username, password, org).GetAwaiter().GetResult();
			});
		}

		private static async Task<int> ExecuteAsync(
			string nupkgPath, string baseUrl,
			string username, string password, string orgName)
		{
			if (!File.Exists(nupkgPath))
			{
				Console.Error.WriteLine($"Package file not found: {nupkgPath}");
				return 1;
			}

			var builder = new ContainerBuilder();
			builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
			var container = builder.Build();

			var context = container.Resolve<IEntitiesContext>();
			var ops = new GalleryOperations(context, container.Resolve<ICredentialBuilder>());

			// 1. Ensure user exists
			// Email must match what SeedFunctionalTestsCommand uses so Playwright login tests work.
			var user = await ops.EnsureUserAsync(username, password, "testnuget@localhost");

			// 2. Ensure org exists
			await ops.EnsureOrganizationAsync(orgName, admin: user, collaborator: null);

			// 3. Create an API key scoped to the org for pushing
			var orgEntity = context.Users.First(u => u.Username == orgName);
			var apiKey = ops.CreateApiKey(
				user, "Seed Single Package",
				scopeActions: new[] { NuGetScopes.PackagePush },
				scopeOwner: orgEntity);
			await context.SaveChangesAsync();

			// 4. Push the package via Gallery HTTP API (updates Lucene index)
			await GalleryOperations.PushPackageAsync(baseUrl, apiKey, nupkgPath);

			return 0;
		}
	}
}
