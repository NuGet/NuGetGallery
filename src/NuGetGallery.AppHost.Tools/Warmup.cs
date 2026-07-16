// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

/// <summary>
/// Sends HTTP requests to warm up an application after startup.
/// Reads the target base URL from the "WarmupBaseUrl" environment variable
/// and the comma-separated paths from "WarmupPaths".
/// </summary>
static class WarmupTool
{
	public static async Task<int> RunAsync(string[] args)
	{
		var baseUrl = Environment.GetEnvironmentVariable("WarmupBaseUrl")
			?? throw new InvalidOperationException("WarmupBaseUrl environment variable is not set.");
		var pathsCsv = Environment.GetEnvironmentVariable("WarmupPaths") ?? "/";

		var paths = pathsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		using var http = new HttpClient(new HttpClientHandler
		{
			// Gallery uses a dev SSL cert; don't fail on self-signed certs.
			ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
		});
		http.Timeout = TimeSpan.FromMinutes(5);

		foreach (var path in paths)
		{
			var url = baseUrl.TrimEnd('/') + path;
			Console.WriteLine($"Warming up: {url}");
			try
			{
				var response = await http.GetAsync(url);
				Console.WriteLine($"  {(int)response.StatusCode} {response.StatusCode} ({url})");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"  Failed: {ex.Message}");
			}
		}

		Console.WriteLine("Warmup complete.");
		return 0;
	}
}
