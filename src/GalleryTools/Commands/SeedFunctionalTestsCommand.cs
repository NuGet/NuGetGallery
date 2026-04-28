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
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Packaging;

namespace GalleryTools.Commands
{
	public static class SeedFunctionalTestsCommand
	{
		public static void Configure(CommandLineApplication config)
		{
			config.Description = "Seed users, organizations, API keys, and a test package for functional tests";
			config.HelpOption("-? | -h | --help");

			var outputOption = config.Option(
				"-o | --output", "Path to write settings.CI.json (required).",
				CommandOptionType.SingleValue);

			var packageOption = config.Option(
				"-p | --package", "Path to the base test .nupkg file (required).",
				CommandOptionType.SingleValue);

			var baseUrlOption = config.Option(
				"--base-url", "Gallery base URL for test config (default: https://localhost).",
				CommandOptionType.SingleValue);

			config.OnExecute(() =>
			{
				if (!outputOption.HasValue() || !packageOption.HasValue())
				{
					Console.WriteLine("--output and --package are required.");
					config.ShowHelp();
					return 1;
				}

				var baseUrl = baseUrlOption.HasValue() ? baseUrlOption.Value() : "https://localhost";
				return ExecuteAsync(outputOption.Value(), packageOption.Value(), baseUrl).GetAwaiter().GetResult();
			});
		}

		private static async Task<int> ExecuteAsync(string outputPath, string nupkgPath, string baseUrl)
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
			var credentialBuilder = container.Resolve<ICredentialBuilder>();
			var packageService = container.Resolve<IPackageService>();
			var packageUploadService = container.Resolve<IPackageUploadService>();

			// ─── Test account names ──────────────────────────────────────────────────
			const string testUser = "NugetTestAccount";
			const string testPassword = "Password1!";
			const string testEmail = "testnuget@localhost";

			const string orgAdminUser = "NugetOrgAdmin";
			const string orgAdminPassword = "Password1!";

			const string adminOrgName = "NugetTestAdminOrganization";
			const string collaboratorOrgName = "NugetTestCollaboratorOrganization";

			// ─── 1. Create users ─────────────────────────────────────────────────────
			var testAccount = await EnsureUserAsync(context, credentialBuilder, testUser, testPassword, testEmail);
			var orgAdmin = await EnsureUserAsync(context, credentialBuilder, orgAdminUser, orgAdminPassword, $"{orgAdminUser}@localhost");

			// ─── 2. Create organizations ─────────────────────────────────────────────
			await EnsureOrganizationAsync(context, adminOrgName, admin: testAccount, collaborator: null);
			await EnsureOrganizationAsync(context, collaboratorOrgName, admin: orgAdmin, collaborator: testAccount);

			// ─── 3. Push base test package ───────────────────────────────────────────
			await EnsurePackageAsync(context, packageService, packageUploadService, testAccount, nupkgPath);

			// ─── 4. Create API keys ──────────────────────────────────────────────────
			var accountApiKey = CreateApiKey(context, credentialBuilder, testAccount, "CI Full Access", scopeActions: null, scopeOwner: testAccount);
			var apiKeyPush = CreateApiKey(context, credentialBuilder, testAccount, "CI Push", scopeActions: new[] { NuGetScopes.PackagePush }, scopeOwner: testAccount);
			var apiKeyPushVersion = CreateApiKey(context, credentialBuilder, testAccount, "CI Push Version", scopeActions: new[] { NuGetScopes.PackagePushVersion }, scopeOwner: testAccount);
			var apiKeyUnlist = CreateApiKey(context, credentialBuilder, testAccount, "CI Unlist", scopeActions: new[] { NuGetScopes.PackageUnlist }, scopeOwner: testAccount);

			// Org API keys: credential on testAccount, scoped to the org
			var adminOrgEntity = context.Users.First(u => u.Username == adminOrgName);
			var collabOrgEntity = context.Users.First(u => u.Username == collaboratorOrgName);

			var adminOrgApiKey = CreateApiKey(context, credentialBuilder, testAccount, "CI Admin Org", scopeActions: null, scopeOwner: adminOrgEntity);
			var collabOrgApiKey = CreateApiKey(context, credentialBuilder, testAccount, "CI Collaborator Org", scopeActions: null, scopeOwner: collabOrgEntity);

			await context.SaveChangesAsync();
			Console.WriteLine("All API keys created.");

			// ─── 5. Write settings.CI.json ───────────────────────────────────────────
			var settings = new JObject(
				new JProperty("DefaultSecurityPoliciesEnforced", true),
				new JProperty("TestPackageLock", false),
				new JProperty("TyposquattingCheckAndBlockUsers", true),
				new JProperty("Branding", new JObject(
					new JProperty("BrandingMessage", "&#169; Microsoft {0}"),
					new JProperty("PrivacyPolicyUrl", "https://go.microsoft.com/fwlink/?LinkId=521839"),
					new JProperty("TrademarksUrl", "https://www.microsoft.com/trademarks"))),
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
				new JProperty("ProductionBaseUrl", baseUrl),
				new JProperty("StagingBaseUrl", ""));

			Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
			File.WriteAllText(outputPath, settings.ToString(Formatting.Indented));
			Console.WriteLine($"Settings written to: {outputPath}");

			return 0;
		}

		private static async Task<User> EnsureUserAsync(
			IEntitiesContext context, ICredentialBuilder credentialBuilder,
			string username, string password, string email)
		{
			var existing = context.Users.FirstOrDefault(u => u.Username == username);
			if (existing != null)
			{
				Console.WriteLine($"User '{username}' already exists (key={existing.Key}).");
				return existing;
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

			Console.WriteLine($"Created user '{username}' (key={user.Key}).");
			return user;
		}

		private static async Task EnsureOrganizationAsync(
			IEntitiesContext context, string orgName, User admin, User collaborator)
		{
			var existing = context.Users.FirstOrDefault(u => u.Username == orgName);
			if (existing != null)
			{
				Console.WriteLine($"Organization '{orgName}' already exists (key={existing.Key}).");
				return;
			}

			var org = new Organization(orgName)
			{
				EmailAllowed = true,
				EmailAddress = $"{orgName}@localhost",
				EmailConfirmationToken = null,
				CreatedUtc = DateTime.UtcNow,
			};

			org.Members.Add(new Membership
			{
				Organization = org,
				Member = admin,
				IsAdmin = true,
			});

			if (collaborator != null)
			{
				org.Members.Add(new Membership
				{
					Organization = org,
					Member = collaborator,
					IsAdmin = false,
				});
			}

			context.Users.Add(org);
			await context.SaveChangesAsync();

			Console.WriteLine($"Created organization '{orgName}' (key={org.Key}, admin={admin.Username}).");
		}

		private static async Task EnsurePackageAsync(
			IEntitiesContext context, IPackageService packageService,
			IPackageUploadService packageUploadService, User owner, string nupkgPath)
		{
			using (var fileStream = File.OpenRead(nupkgPath))
			using (var packageArchiveReader = new PackageArchiveReader(fileStream, leaveStreamOpen: true))
			{
				var nuspec = packageArchiveReader.GetNuspecReader();
				var id = nuspec.GetId();
				var version = nuspec.GetVersion().ToNormalizedString();

				var existingPackage = packageService.FindPackageByIdAndVersionStrict(id, version);
				if (existingPackage != null)
				{
					Console.WriteLine($"Package {id} {version} already exists (key={existingPackage.Key}).");
					return;
				}

				fileStream.Position = 0;
				var packageStreamMetadata = new PackageStreamMetadata
				{
					HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
					Hash = CryptographyService.GenerateHash(fileStream, CoreConstants.Sha512HashAlgorithmId),
					Size = fileStream.Length,
				};

				fileStream.Position = 0;
				var package = await packageUploadService.GeneratePackageAsync(
					id, packageArchiveReader, packageStreamMetadata, owner, owner);

				fileStream.Position = 0;
				var commitResult = await packageUploadService.CommitPackageAsync(package, fileStream);

				if (commitResult == PackageCommitResult.Success)
				{
					Console.WriteLine($"Pushed {id} {version} (owner={owner.Username}, key={package.Key}).");
				}
				else
				{
					throw new InvalidOperationException($"Failed to commit package {id} {version}: {commitResult}");
				}
			}
		}

		/// <summary>
		/// Creates an API key credential and returns the plaintext key.
		/// Does NOT call SaveChangesAsync — caller should batch saves.
		/// </summary>
		private static string CreateApiKey(
			IEntitiesContext context, ICredentialBuilder credentialBuilder,
			User user, string description, string[] scopeActions, User scopeOwner)
		{
			var credential = credentialBuilder.CreateApiKey(expiration: null, out string plaintextApiKey);
			credential.Description = description;
			credential.User = user;
			credential.UserKey = user.Key;
			credential.Scopes = credentialBuilder.BuildScopes(scopeOwner, scopeActions, subjects: null);

			user.Credentials.Add(credential);

			Console.WriteLine($"Created API key '{description}' for '{user.Username}' (scope owner={scopeOwner.Username}).");
			return plaintextApiKey;
		}
	}
}
