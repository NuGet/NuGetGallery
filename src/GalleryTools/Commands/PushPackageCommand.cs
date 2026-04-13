// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Packaging;
using NuGetGallery.Infrastructure.Authentication;

namespace GalleryTools.Commands
{
	public static class PushPackageCommand
	{
		public static void Configure (CommandLineApplication config)
		{
			config.Description = "Upload a .nupkg to the Gallery on behalf of a user, creating an API key automatically";
			config.HelpOption("-? | -h | --help");

			var ownerOption = config.Option(
				"-o | --owner", "Username of the package owner (required).",
				CommandOptionType.SingleValue);

			var packageOption = config.Option(
				"-p | --package", "Path to the .nupkg file (required).",
				CommandOptionType.SingleValue);

			config.OnExecute(() =>
			{
				if (!ownerOption.HasValue() || !packageOption.HasValue())
				{
					Console.WriteLine("--owner and --package are required.");
					config.ShowHelp();
					return 1;
				}

				return ExecuteAsync(ownerOption.Value(), packageOption.Value()).GetAwaiter().GetResult();
			});
		}

		private static async Task<int> ExecuteAsync (string ownerUsername, string nupkgPath)
		{
			if (!File.Exists(nupkgPath))
			{
				Console.WriteLine($"Package file not found: {nupkgPath}");
				return 1;
			}

			var builder = new ContainerBuilder();
			builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
			var container = builder.Build();

			var context = container.Resolve<IEntitiesContext>();
			var packageService = container.Resolve<IPackageService>();
			var packageUploadService = container.Resolve<IPackageUploadService>();

			// Look up the owner user.
			var owner = context.Users.FirstOrDefault(u => u.Username == ownerUsername);
			if (owner == null)
			{
				Console.WriteLine($"User '{ownerUsername}' not found. Run 'createuser' first.");
				return 1;
			}

			using (var fileStream = File.OpenRead(nupkgPath))
			using (var packageArchiveReader = new PackageArchiveReader(fileStream, leaveStreamOpen: true))
			{
				var nuspec = packageArchiveReader.GetNuspecReader();
				var id = nuspec.GetId();
				var version = nuspec.GetVersion().ToNormalizedString();

				// Check if this exact package version already exists.
				var existingPackage = packageService.FindPackageByIdAndVersionStrict(id, version);
				if (existingPackage != null)
				{
					Console.WriteLine($"Package {id} {version} already exists (key={existingPackage.Key}). Skipping upload.");
					return 0;
				}

				// Compute stream metadata (hash + size) — same as ApiController.CreatePackagePut.
				fileStream.Position = 0;
				var packageStreamMetadata = new PackageStreamMetadata
				{
					HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
					Hash = CryptographyService.GenerateHash(fileStream, CoreConstants.Sha512HashAlgorithmId),
					Size = fileStream.Length,
				};

				// Create the Package entity via the upload service layer. GeneratePackageAsync
				// wraps PackageService.CreatePackageAsync and also handles reserved namespace
				// verification and applying existing vulnerabilities.
				fileStream.Position = 0;
				var package = await packageUploadService.GeneratePackageAsync(
					id,
					packageArchiveReader,
					packageStreamMetadata,
					owner,
					owner);

				// Save the nupkg blob and commit to DB.
				fileStream.Position = 0;
				var commitResult = await packageUploadService.CommitPackageAsync(package, fileStream);

				if (commitResult == PackageCommitResult.Success)
				{
					Console.WriteLine($"Pushed {id} {version} (owner={ownerUsername}, key={package.Key}).");
					return 0;
				}
				else
				{
					Console.WriteLine($"Failed to commit package {id} {version}: {commitResult}");
					return 1;
				}
			}
		}
	}
}
