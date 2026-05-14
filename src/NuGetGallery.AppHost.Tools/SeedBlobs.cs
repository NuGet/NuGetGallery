// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Seeds blobs in Azurite that the Gallery and pipeline jobs expect to exist:
/// auxiliary blobs, Gallery content files from App_Data, and the V3 service index.
/// </summary>
static class SeedBlobsTool
{
	public static async Task<int> RunAsync(string[] args)
	{
		var cfg = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.Build()
			.Get<NuGetGalleryConfig>()!;

		var blobService = new BlobServiceClient(cfg.StorageConnectionString);
		var searchBase = cfg.SearchServiceBaseAddress.TrimEnd('/');

		// ── Auxiliary blobs for pipeline jobs ────────────────────────────────
		await SeedAsync(blobService, cfg.Containers.CdnStats, cfg.AuxiliaryBlobs.DownloadsV1Json,
			"""[["_placeholder",["0.0.0",1]]]""");

		await SeedAsync(blobService, cfg.Containers.SearchAuxiliary, cfg.AuxiliaryBlobs.ExcludedPackagesJson,
			"[]");

		// ── Gallery content files from App_Data ─────────────────────────────
		var repoRoot = Environment.GetEnvironmentVariable("REPO_ROOT")
			?? throw new InvalidOperationException("REPO_ROOT environment variable is not set.");
		var contentDir = Path.Combine(repoRoot, "src", "NuGetGallery", "App_Data", "Files", "Content");

		if (Directory.Exists(contentDir))
		{
			Console.WriteLine($"Seeding Gallery content from {contentDir}");
			var seededFlags = false;
			foreach (var file in Directory.GetFiles(contentDir))
			{
				var fileName = Path.GetFileName(file);
				if (fileName.Equals("flags.json", StringComparison.OrdinalIgnoreCase))
				{
					seededFlags = true;
				}
				var contentType = Path.GetExtension(file).ToLowerInvariant() switch
				{
					".json" => "application/json",
					".md" => "text/markdown",
					".html" => "text/html",
					_ => "application/octet-stream",
				};
				await SeedFileAsync(blobService, cfg.Containers.Content, fileName, file, contentType);
			}
			if (!seededFlags)
				throw new FileNotFoundException(
					"flags.json not found in content directory. The Gallery requires this file.",
					contentDir);
		}
		else
		{
			throw new DirectoryNotFoundException(
				$"Content directory not found at {contentDir}. Cannot seed Gallery content blobs.");
		}

		// ── V3 service index ────────────────────────────────────────────────
		await SeedAsync(blobService, cfg.Containers.ServiceIndex, "index.json", $$"""
{
  "version": "3.0.0",
  "resources": [
    {
      "@id": "{{cfg.AzuriteBase}}/{{cfg.Containers.Catalog}}/index.json",
      "@type": "Catalog/3.0.0",
      "comment": "Index of the NuGet package catalog."
    },
    {
      "@id": "{{cfg.AzuriteBase}}/{{cfg.Containers.FlatContainer}}/",
      "@type": "PackageBaseAddress/3.0.0",
      "comment": "Base URL of where NuGet packages are stored."
    },
    {
      "@id": "{{cfg.AzuriteBase}}/{{cfg.Containers.RegistrationSemVer1}}/",
      "@type": "RegistrationsBaseUrl",
      "comment": "Base URL of NuGet package registration info."
    },
    {
      "@id": "{{cfg.AzuriteBase}}/{{cfg.Containers.RegistrationSemVer1}}/",
      "@type": "RegistrationsBaseUrl/3.0.0-rc",
      "comment": "Base URL of NuGet package registration info (SemVer 1.0)."
    },
    {
      "@id": "{{cfg.AzuriteBase}}/{{cfg.Containers.RegistrationGzSemVer1}}/",
      "@type": "RegistrationsBaseUrl/3.4.0",
      "comment": "Base URL of NuGet package registration info in GZIP format (SemVer 1.0)."
    },
    {
      "@id": "{{cfg.AzuriteBase}}/{{cfg.Containers.RegistrationGzSemVer2}}/",
      "@type": "RegistrationsBaseUrl/3.6.0",
      "comment": "Base URL of NuGet package registration info in GZIP format (SemVer 2.0)."
    },
    {
      "@id": "{{cfg.AzuriteBase}}/{{cfg.Containers.RegistrationGzSemVer2}}/",
      "@type": "RegistrationsBaseUrl/Versioned",
      "comment": "Base URL of NuGet package registration info in GZIP format (SemVer 2.0)."
    },
    {
      "@id": "{{searchBase}}/query",
      "@type": "SearchQueryService",
      "comment": "Query endpoint of NuGet Search service."
    },
    {
      "@id": "{{searchBase}}/query",
      "@type": "SearchQueryService/3.0.0-rc"
    },
    {
      "@id": "{{searchBase}}/query",
      "@type": "SearchQueryService/3.5.0"
    },
    {
      "@id": "{{searchBase}}/autocomplete",
      "@type": "SearchAutocompleteService",
      "comment": "Autocomplete endpoint of NuGet Search service."
    },
    {
      "@id": "{{searchBase}}/autocomplete",
      "@type": "SearchAutocompleteService/3.0.0-rc"
    },
    {
      "@id": "{{searchBase}}/autocomplete",
      "@type": "SearchAutocompleteService/3.5.0"
    },
    {
      "@id": "{{cfg.GalleryBaseAddress}}/api/v2/package",
      "@type": "PackagePublish/2.0.0"
    },
    {
      "@id": "{{cfg.GalleryBaseAddress}}/packages/{id}/{version}?_src=template",
      "@type": "PackageDetailsUriTemplate/5.1.0",
      "comment": "URI template for package details page."
    },
    {
      "@id": "{{cfg.GalleryBaseAddress}}/packages/{id}/{version}/ReportAbuse",
      "@type": "ReportAbuseUriTemplate/3.0.0-rc",
      "comment": "URI template for reporting package abuse."
    },
    {
      "@id": "{{cfg.AzuriteBase}}/{{cfg.Containers.FlatContainer}}/{lower_id}/{lower_version}/readme",
      "@type": "ReadmeUriTemplate/6.13.0",
      "comment": "URI template for downloading a package's README."
    }
  ],
  "@context": {
    "@vocab": "http://schema.nuget.org/services#",
    "comment": "http://www.w3.org/2000/01/rdf-schema#comment"
  }
}
""");

		Console.WriteLine("Seeded blobs.");
		return 0;
	}

	static async Task SeedAsync(BlobServiceClient blobService,
		string containerName, string blobName, string content)
	{
		var container = blobService.GetBlobContainerClient(containerName);
		await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
		var blob = container.GetBlobClient(blobName);
		await blob.UploadAsync(
			new BinaryData(content),
			new BlobUploadOptions
			{
				HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
			},
			cancellationToken: default);
		Console.WriteLine($"  {containerName}/{blobName}");
	}

	static async Task SeedFileAsync(BlobServiceClient blobService,
		string containerName, string blobName, string filePath, string contentType)
	{
		var container = blobService.GetBlobContainerClient(containerName);
		await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
		var blob = container.GetBlobClient(blobName);
		await using var stream = File.OpenRead(filePath);
		await blob.UploadAsync(
			stream,
			new BlobUploadOptions
			{
				HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
			},
			cancellationToken: default);
		Console.WriteLine($"  {containerName}/{blobName}");
	}
}
