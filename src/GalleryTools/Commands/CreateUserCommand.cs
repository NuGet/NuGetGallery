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
	public static class CreateUserCommand
	{
		public static void Configure (CommandLineApplication config)
		{
			config.Description = "Create a local Gallery user with a confirmed email address";
			config.HelpOption("-? | -h | --help");

			var usernameOption = config.Option(
				"-u | --username", "Username for the new user (required).",
				CommandOptionType.SingleValue);

			var passwordOption = config.Option(
				"-p | --password", "Password for the new user (required).",
				CommandOptionType.SingleValue);

			var emailOption = config.Option(
				"-e | --email", "Email address (defaults to {username}@localhost).",
				CommandOptionType.SingleValue);

			config.OnExecute(() =>
			{
				if (!usernameOption.HasValue() || !passwordOption.HasValue())
				{
					Console.WriteLine("--username and --password are required.");
					config.ShowHelp();
					return 1;
				}

				var username = usernameOption.Value();
				var password = passwordOption.Value();
				var email = emailOption.HasValue()
					? emailOption.Value()
					: $"{username}@localhost";

				return ExecuteAsync(username, password, email).GetAwaiter().GetResult();
			});
		}

		private static async Task<int> ExecuteAsync (string username, string password, string email)
		{
			var builder = new ContainerBuilder();
			builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
			var container = builder.Build();

			var context = container.Resolve<IEntitiesContext>();
			var credentialBuilder = container.Resolve<ICredentialBuilder>();

			// Check if user already exists.
			var existingUser = context.Users
				.FirstOrDefault(u => u.Username == username);

			if (existingUser != null)
			{
				Console.WriteLine($"User '{username}' already exists (key={existingUser.Key}). Skipping creation.");
				return 0;
			}

			var passwordCredential = credentialBuilder.CreatePasswordCredential(password);

			var user = new User(username)
			{
				EmailAllowed = true,
				EmailAddress = email,
				EmailConfirmationToken = null,
				NotifyPackagePushed = true,
				CreatedUtc = DateTime.UtcNow,
			};

			user.Credentials.Add(passwordCredential);

			context.Users.Add(user);
			await context.SaveChangesAsync();

			Console.WriteLine($"Created user '{username}' (key={user.Key}, email={email}, confirmed=true).");
			return 0;
		}
	}
}
