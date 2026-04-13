// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Polls Azure Search for the existence of search indexes and the cursor.json blob in Azurite.
// Exits 0 once all artifacts are present, allowing dependent Aspire resources to start.
// This decouples dependents from db2azuresearch's exit code — if indexes already exist from
// a prior run, dependents start immediately even when db2azuresearch fails on "already exists".
// Configuration is read from environment variables set by the Aspire AppHost.

#:package Azure.Search.Documents
#:package Azure.Identity
#:package Azure.Storage.Blobs
#:package Microsoft.Extensions.Configuration
#:package Microsoft.Extensions.Configuration.Binder
#:package Microsoft.Extensions.Configuration.EnvironmentVariables
#:project ../NuGetGallery.AppHost.Config/NuGetGallery.AppHost.Config.csproj

using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
	.AddEnvironmentVariables()
	.Build();

var searchServiceName      = config["SearchServiceName"]!;
var storageConnectionString = config["StorageConnectionString"]!;
var searchIndexes          = config.GetSection("SearchIndexes").Get<SearchIndexNames>()!;
var containers             = config.GetSection("Containers").Get<ContainerNames>()!;

var endpoint = new Uri($"https://{searchServiceName}.search.windows.net");
var indexClient = new SearchIndexClient(endpoint, new DefaultAzureCredential());
var blobService = new BlobServiceClient(storageConnectionString);

while (true)
{
	try
	{
		await indexClient.GetIndexAsync(cfg.SearchIndexes.Search);
		await indexClient.GetIndexAsync(cfg.SearchIndexes.Hijack);

		var container = blobService.GetBlobContainerClient(cfg.Containers.AzureSearch);
		var cursor = container.GetBlobClient("cursor.json");
		if (await cursor.ExistsAsync())
		{
			Console.WriteLine("Search indexes and cursor.json are ready.");
			return;
		}

		Console.WriteLine("Waiting for cursor.json...");
	}
	catch
	{
		Console.WriteLine("Waiting for search indexes...");
	}

	await Task.Delay(TimeSpan.FromSeconds(5));
}
