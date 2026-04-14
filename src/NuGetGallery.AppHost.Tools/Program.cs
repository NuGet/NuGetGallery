// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Dispatcher for AppHost tools. The first argument selects the tool to run.
// This project replaces the previous single-file scripts (dotnet run *.cs) so
// that compilation errors are caught at build time rather than at launch time.

if (args.Length == 0)
{
	Console.Error.WriteLine("Usage: NuGetGallery.AppHost.Tools <command> [args]");
	Console.Error.WriteLine("Commands: seed-blobs, catalog-index-available, search-index-available, warmup");
	return 1;
}

var command = args[0].ToLowerInvariant();
var remaining = args[1..];

return command switch
{
	"seed-blobs" => await SeedBlobsTool.RunAsync(remaining),
	"catalog-index-available" => await V3CatalogIndexAvailableTool.RunAsync(remaining),
	"search-index-available" => await SearchIndexAvailableTool.RunAsync(remaining),
	"warmup" => await WarmupTool.RunAsync(remaining),
	_ => throw new ArgumentException($"Unknown command: {command}"),
};
