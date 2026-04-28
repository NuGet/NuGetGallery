// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
			var credentialBuilder = container.Resolve<ICredentialBuilder>();

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

			// ─── 3. Create API keys ──────────────────────────────────────────────────
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

			// ─── 4. Push test packages via Gallery API ──────────────────────────────
			// Push through the gallery's HTTP API so the in-process Lucene index
			// is updated automatically by ApiController.UpdatePackage().
			foreach (var nupkgPath in nupkgFiles)
			{
				await PushPackageViaApiAsync(baseUrl, apiKeyPush, nupkgPath);
			}

			// ─── 5. Write settings.CI.json ───────────────────────────────────────────
			var settings = new JObject(
				new JProperty("DefaultSecurityPoliciesEnforced", true),
				new JProperty("TestPackageLock", true),
				new JProperty("TyposquattingCheckAndBlockUsers", true),
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

		/// <summary>
		/// Pushes a .nupkg.testdata file to the gallery via the API push endpoint.
		/// This ensures the gallery's in-process Lucene index is updated automatically.
		/// </summary>
		private static async Task PushPackageViaApiAsync(string baseUrl, string apiKey, string nupkgPath)
		{
			var fileName = Path.GetFileName(nupkgPath);

			// Trust dev certs for localhost HTTPS
			var handler = new HttpClientHandler();
			if (baseUrl.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true;
			}

			using (var client = new HttpClient(handler))
			{
				client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", apiKey);
				client.Timeout = TimeSpan.FromSeconds(120);

				using (var fileStream = File.OpenRead(nupkgPath))
				{
					var content = new MultipartFormDataContent();
					var streamContent = new StreamContent(fileStream);
					streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					content.Add(streamContent, "package", fileName);

					var pushUrl = $"{baseUrl.TrimEnd('/')}/api/v2/package";
					var response = await client.PutAsync(pushUrl, content);

					if (response.StatusCode == HttpStatusCode.Conflict)
					{
						Console.WriteLine($"Package {fileName} already exists (409 Conflict). Skipping.");
						return;
					}

					if (response.StatusCode == HttpStatusCode.Forbidden)
					{
						// Package may already exist under a different owner (e.g. seeded by AppHost).
						Console.WriteLine($"Package {fileName} returned 403 Forbidden (likely already owned by another account). Skipping.");
						return;
					}

					if (!response.IsSuccessStatusCode)
					{
						var body = await response.Content.ReadAsStringAsync();
						throw new InvalidOperationException(
							$"Failed to push {fileName}: {response.StatusCode} {response.ReasonPhrase}\n{body}");
					}

					Console.WriteLine($"Pushed {fileName} via API ({response.StatusCode}).");
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
