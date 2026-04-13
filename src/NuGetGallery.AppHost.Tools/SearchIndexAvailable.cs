// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Polls Azure Search for the existence of search indexes and the cursor.json blob in Azurite.
/// Exits 0 once all artifacts are present, allowing dependent Aspire resources to start.
/// </summary>
static class SearchIndexAvailableTool
{
	public static async Task<int> RunAsync (string[] args)
	{
		var config = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.Build();

		var searchServiceName       = config["SearchServiceName"]!;
		var storageConnectionString = config["StorageConnectionString"]!;
		var searchIndexes           = config.GetSection("SearchIndexes").Get<SearchIndexNames>()!;
		var containers              = config.GetSection("Containers").Get<ContainerNames>()!;

		var endpoint = new Uri($"https://{searchServiceName}.search.windows.net");
		var indexClient = new SearchIndexClient(endpoint, new DefaultAzureCredential());
		var blobService = new BlobServiceClient(storageConnectionString);

		while (true)
		{
			try
			{
				await indexClient.GetIndexAsync(searchIndexes.Search);
				await indexClient.GetIndexAsync(searchIndexes.Hijack);

				var container = blobService.GetBlobContainerClient(containers.AzureSearch);
				var cursor = container.GetBlobClient("cursor.json");
				if (await cursor.ExistsAsync())
				{
					Console.WriteLine("Search indexes and cursor.json are ready.");
					return 0;
				}

				Console.WriteLine("Waiting for cursor.json...");
			}
			catch
			{
				Console.WriteLine("Waiting for search indexes...");
			}

			await Task.Delay(TimeSpan.FromSeconds(5));
		}
	}
}
