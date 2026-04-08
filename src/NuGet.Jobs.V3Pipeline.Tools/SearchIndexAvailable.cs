// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Polls Azure Search for the existence of search indexes and the cursor.json blob in Azurite.
// Exits 0 once all artifacts are present, allowing dependent Aspire resources to start.
// This decouples dependents from db2azuresearch's exit code — if indexes already exist from
// a prior run, dependents start immediately even when db2azuresearch fails on "already exists".
// Usage: dotnet run SearchIndexAvailable.cs -- <searchServiceName> <storageConnectionString>

#:package Azure.Search.Documents
#:package Azure.Identity
#:package Azure.Storage.Blobs

using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;

var searchServiceName = args[0];
var storageConnectionString = args[1];

var endpoint = new Uri($"https://{searchServiceName}.search.windows.net");
var indexClient = new SearchIndexClient(endpoint, new DefaultAzureCredential());
var blobService = new BlobServiceClient(storageConnectionString);

while (true)
{
	try
	{
		await indexClient.GetIndexAsync("search-index");
		await indexClient.GetIndexAsync("hijack-index");

		var container = blobService.GetBlobContainerClient("v3-azuresearch0");
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
