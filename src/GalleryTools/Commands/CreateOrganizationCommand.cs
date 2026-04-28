// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Infrastructure.Authentication;

namespace GalleryTools.Commands
{
	public static class CreateOrganizationCommand
	{
		public static void Configure(CommandLineApplication config)
		{
			config.Description = "Create a local Gallery organization with admin and optional collaborator memberships";
			config.HelpOption("-? | -h | --help");

			var nameOption = config.Option(
				"-n | --name", "Organization name (required).",
				CommandOptionType.SingleValue);

			var adminOption = config.Option(
				"-a | --admin", "Username of the organization administrator (required).",
				CommandOptionType.SingleValue);

			var collaboratorOption = config.Option(
				"-c | --collaborator", "Username of a collaborator member (optional).",
				CommandOptionType.SingleValue);

			config.OnExecute(() =>
			{
				if (!nameOption.HasValue() || !adminOption.HasValue())
				{
					Console.WriteLine("--name and --admin are required.");
					config.ShowHelp();
					return 1;
				}

				return ExecuteAsync(
					nameOption.Value(),
					adminOption.Value(),
					collaboratorOption.Value()).GetAwaiter().GetResult();
			});
		}

		private static async Task<int> ExecuteAsync(
			string orgName, string adminUsername, string collaboratorUsername)
		{
			var builder = new ContainerBuilder();
			builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
			var container = builder.Build();

			var context = container.Resolve<IEntitiesContext>();

			// Check if organization already exists.
			var existingOrg = context.Users.FirstOrDefault(u => u.Username == orgName);
			if (existingOrg != null)
			{
				Console.WriteLine($"Organization '{orgName}' already exists (key={existingOrg.Key}). Skipping creation.");
				return 0;
			}

			// Look up admin user.
			var admin = context.Users.FirstOrDefault(u => u.Username == adminUsername);
			if (admin == null)
			{
				Console.Error.WriteLine($"Admin user '{adminUsername}' not found. Run 'createuser' first.");
				return 1;
			}

			// Create the organization.
			var org = new Organization(orgName)
			{
				EmailAllowed = true,
				EmailAddress = $"{orgName}@localhost",
				EmailConfirmationToken = null,
				CreatedUtc = DateTime.UtcNow,
			};

			// Add admin membership.
			org.Members.Add(new Membership
			{
				Organization = org,
				Member = admin,
				IsAdmin = true,
			});

			// Add collaborator membership if specified.
			if (!string.IsNullOrEmpty(collaboratorUsername))
			{
				var collaborator = context.Users.FirstOrDefault(u => u.Username == collaboratorUsername);
				if (collaborator == null)
				{
					Console.Error.WriteLine($"Collaborator user '{collaboratorUsername}' not found. Run 'createuser' first.");
					return 1;
				}

				org.Members.Add(new Membership
				{
					Organization = org,
					Member = collaborator,
					IsAdmin = false,
				});
			}

			context.Users.Add(org);
			await context.SaveChangesAsync();

			Console.WriteLine($"Created organization '{orgName}' (key={org.Key}, admin={adminUsername}).");
			return 0;
		}
	}
}
