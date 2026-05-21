// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

/// <summary>
/// Polls for the V3 catalog index.json blob in Azurite.
/// Exits 0 once the blob returns HTTP 200, allowing dependent Aspire resources to start.
/// </summary>
static class V3CatalogIndexAvailableTool
{
	public static async Task<int> RunAsync(string[] args)
	{
		var cfg = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.Build()
			.Get<NuGetGalleryConfig>()!;

		using var http = new HttpClient();
		var url = $"{cfg.AzuriteBase}/{cfg.Containers.Catalog}/index.json";

		while (true)
		{
			try
			{
				var response = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
				if (response.IsSuccessStatusCode)
				{
					Console.WriteLine($"Catalog index found at {url}");
					return 0;
				}
			}
			catch
			{
				// Blob doesn't exist yet — keep polling.
			}

			Console.WriteLine("Waiting for catalog index...");
			await Task.Delay(TimeSpan.FromSeconds(5));
		}
	}
}
