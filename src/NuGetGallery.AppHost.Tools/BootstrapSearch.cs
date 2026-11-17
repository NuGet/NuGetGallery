// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using Azure;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Runs the initial Search bootstrap only when local indexes and the catalog cursor do not exist.
/// </summary>
static class BootstrapSearchTool
{
	public static async Task<int> RunAsync(string[] args)
	{
		if (args.Length != 2)
		{
			throw new ArgumentException("Usage: bootstrap-search <Db2AzureSearch executable> <configuration file>");
		}

		var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
		var searchServiceName = config["SearchServiceName"]
			?? throw new InvalidOperationException("SearchServiceName is not configured.");
		var storageConnectionString = config["StorageConnectionString"]
			?? throw new InvalidOperationException("StorageConnectionString is not configured.");
		var indexes = config.GetSection("SearchIndexes").Get<SearchIndexNames>()
			?? throw new InvalidOperationException("SearchIndexes is not configured.");
		var containers = config.GetSection("Containers").Get<ContainerNames>()
			?? throw new InvalidOperationException("Containers is not configured.");
		var indexClient = new SearchIndexClient(new Uri($"https://{searchServiceName}.search.windows.net"), new DefaultAzureCredential());
		var cursor = new BlobServiceClient(storageConnectionString).GetBlobContainerClient(containers.AzureSearch).GetBlobClient("cursor.json");

		return await RunAsync(indexClient, cursor, indexes, () => RunBootstrapAsync(args[0], args[1]));
	}

	internal static async Task<int> RunAsync(SearchIndexClient indexClient, BlobClient cursor, SearchIndexNames indexes, Func<Task<int>> bootstrap)
	{
		var searchExists = await IndexExistsAsync(indexClient, indexes.Search);
		var hijackExists = await IndexExistsAsync(indexClient, indexes.Hijack);
		var cursorExists = (await cursor.ExistsAsync()).Value;

		// Db2AzureSearch writes the cursor only after the initial indexes and auxiliary files are complete.
		if (searchExists && hijackExists && cursorExists)
		{
			Console.WriteLine("Search is already initialized. Skipping database bootstrap and preserving the catalog cursor.");
			return 0;
		}

		if (searchExists || hijackExists || cursorExists)
		{
			throw new InvalidOperationException(
				"Local Search bootstrap is incomplete. Both indexes and cursor.json must exist together. " +
				"Restore the matching state or explicitly reset the local V3 pipeline before retrying.");
		}

		Console.WriteLine("Initializing Search from the database.");
		return await bootstrap();
	}

	private static async Task<bool> IndexExistsAsync(SearchIndexClient client, string name)
	{
		try
		{
			await client.GetIndexAsync(name);
			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return false;
		}
	}

	private static async Task<int> RunBootstrapAsync(string executablePath, string configurationPath)
	{
		var startInfo = new ProcessStartInfo(Path.GetFullPath(executablePath))
		{
			WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath))!,
			UseShellExecute = false,
		};
		startInfo.ArgumentList.Add("-Configuration");
		startInfo.ArgumentList.Add(Path.GetFullPath(configurationPath));
		startInfo.ArgumentList.Add("-Once");
		startInfo.ArgumentList.Add("true");

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start Db2AzureSearch.");
		await process.WaitForExitAsync();
		return process.ExitCode;
	}
}
