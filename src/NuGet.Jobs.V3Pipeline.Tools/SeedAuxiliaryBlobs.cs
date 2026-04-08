// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Seeds auxiliary blobs in Azurite that pipeline jobs expect to exist before first run.
// Creates containers (if missing) and uploads placeholder data for:
//   - nuget-cdnstats/downloads.v1.json
//   - search-auxiliary/ExcludedPackages.v1.json
//   - content/flags.json
//   - v3-index/index.json  (V3 service index pointing to local endpoints)
// Usage: dotnet run SeedAuxiliaryBlobs.cs -- <storageConnectionString> <azuriteBaseUrl> <galleryBaseAddress>

#:package Azure.Storage.Blobs

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

var connectionString = args[0];
var azuriteBase = args[1];
var galleryBase = args[2];
var searchBase = "http://127.0.0.1:5200";

var blobService = new BlobServiceClient(connectionString);

await SeedAsync("nuget-cdnstats", "downloads.v1.json",
	"""[["_placeholder",["0.0.0",1]]]""");

await SeedAsync("search-auxiliary", "ExcludedPackages.v1.json",
	"[]");

await SeedAsync("content", "flags.json",
	"""{"Features":{},"Flights":{}}""");

await SeedAsync("v3-index", "index.json", $$"""
{
  "version": "3.0.0",
  "resources": [
    {
      "@id": "{{azuriteBase}}/v3-catalog0/index.json",
      "@type": "Catalog/3.0.0",
      "comment": "Index of the NuGet package catalog."
    },
    {
      "@id": "{{azuriteBase}}/v3-flatcontainer/",
      "@type": "PackageBaseAddress/3.0.0",
      "comment": "Base URL of where NuGet packages are stored."
    },
    {
      "@id": "{{azuriteBase}}/v3-registration5-semver1/",
      "@type": "RegistrationsBaseUrl",
      "comment": "Base URL of NuGet package registration info."
    },
    {
      "@id": "{{azuriteBase}}/v3-registration5-semver1/",
      "@type": "RegistrationsBaseUrl/3.0.0-rc",
      "comment": "Base URL of NuGet package registration info (SemVer 1.0)."
    },
    {
      "@id": "{{azuriteBase}}/v3-registration5-gz-semver1/",
      "@type": "RegistrationsBaseUrl/3.4.0",
      "comment": "Base URL of NuGet package registration info in GZIP format (SemVer 1.0)."
    },
    {
      "@id": "{{azuriteBase}}/v3-registration5-gz-semver2/",
      "@type": "RegistrationsBaseUrl/3.6.0",
      "comment": "Base URL of NuGet package registration info in GZIP format (SemVer 2.0)."
    },
    {
      "@id": "{{azuriteBase}}/v3-registration5-gz-semver2/",
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
      "@id": "{{galleryBase}}/api/v2/package",
      "@type": "PackagePublish/2.0.0"
    },
    {
      "@id": "{{galleryBase}}/packages/{id}/{version}?_src=template",
      "@type": "PackageDetailsUriTemplate/5.1.0",
      "comment": "URI template for package details page."
    },
    {
      "@id": "{{galleryBase}}/packages/{id}/{version}/ReportAbuse",
      "@type": "ReportAbuseUriTemplate/3.0.0-rc",
      "comment": "URI template for reporting package abuse."
    },
    {
      "@id": "{{azuriteBase}}/v3-flatcontainer/{lower_id}/{lower_version}/readme",
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

Console.WriteLine("Seeded auxiliary blobs.");

async Task SeedAsync (string containerName, string blobName, string content)
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
