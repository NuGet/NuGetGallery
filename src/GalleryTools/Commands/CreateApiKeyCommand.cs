// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;

namespace GalleryTools.Commands
{
	public static class CreateApiKeyCommand
	{
		public static void Configure(CommandLineApplication config)
		{
			config.Description = "Create an API key for a local Gallery user and print the plaintext key to stdout";
			config.HelpOption("-? | -h | --help");

			var userOption = config.Option(
				"-u | --user", "Username of the key owner (required).",
				CommandOptionType.SingleValue);

			var descriptionOption = config.Option(
				"-d | --description", "Description for the API key (required).",
				CommandOptionType.SingleValue);

			var scopeOption = config.Option(
				"-s | --scope", "Scope: push, pushversion, unlist, or all (default: all).",
				CommandOptionType.SingleValue);

			var ownerScopeOption = config.Option(
				"--owner-scope", "Username of the scope owner (e.g. an organization). Defaults to the key owner.",
				CommandOptionType.SingleValue);

			config.OnExecute(() =>
			{
				if (!userOption.HasValue() || !descriptionOption.HasValue())
				{
					Console.WriteLine("--user and --description are required.");
					config.ShowHelp();
					return 1;
				}

				var scope = scopeOption.HasValue() ? scopeOption.Value() : "all";
				return ExecuteAsync(
					userOption.Value(),
					descriptionOption.Value(),
					scope,
					ownerScopeOption.Value()).GetAwaiter().GetResult();
			});
		}

		private static async Task<int> ExecuteAsync(
			string username, string description, string scope, string ownerScopeUsername)
		{
			var builder = new ContainerBuilder();
			builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
			var container = builder.Build();

			var context = container.Resolve<IEntitiesContext>();
			var ops = new GalleryOperations(context, container.Resolve<ICredentialBuilder>());

			var user = context.Users.FirstOrDefault(u => u.Username == username);
			if (user == null)
			{
				Console.Error.WriteLine($"User '{username}' not found. Run 'createuser' first.");
				return 1;
			}

			// Determine the scope owner (defaults to the key owner).
			User scopeOwner = user;
			if (!string.IsNullOrEmpty(ownerScopeUsername))
			{
				scopeOwner = context.Users.FirstOrDefault(u => u.Username == ownerScopeUsername);
				if (scopeOwner == null)
				{
					Console.Error.WriteLine($"Scope owner '{ownerScopeUsername}' not found.");
					return 1;
				}
			}

			// Map scope string to NuGetScopes constants.
			string[] scopeActions = MapScope(scope, out bool validScope);
			if (!validScope)
			{
				Console.Error.WriteLine($"Invalid scope '{scope}'. Use: push, pushversion, unlist, or all.");
				return 1;
			}

			// Create the API key credential.
			var plaintextApiKey = ops.CreateApiKey(user, description, scopeActions, scopeOwner);
			await context.SaveChangesAsync();

			// Print only the plaintext key to stdout so callers can capture it.
			Console.WriteLine(plaintextApiKey);
			return 0;
		}

		/// <summary>
		/// Returns scope actions, or empty array for invalid input.
		/// null = all scopes (BuildScopes treats null as "all").
		/// </summary>
		private static string[] MapScope(string scope, out bool valid)
		{
			valid = true;
			switch (scope.ToLowerInvariant())
			{
				case "all":
					return null;
				case "push":
					return new[] { NuGetScopes.PackagePush };
				case "pushversion":
					return new[] { NuGetScopes.PackagePushVersion };
				case "unlist":
					return new[] { NuGetScopes.PackageUnlist };
				default:
					valid = false;
					return null;
			}
		}
	}
}
