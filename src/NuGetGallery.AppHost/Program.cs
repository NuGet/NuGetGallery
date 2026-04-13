// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
var config = builder.Configuration.GetSection("NuGetGallery").Get<NuGetGalleryConfig>()!;

// Read the Aspire-provisioned search service name from deployment outputs in user secrets.
var searchOutputsJson = builder.Configuration["Azure:Deployments:search:Outputs"];
var searchServiceName = "";
if (!string.IsNullOrEmpty(searchOutputsJson))
{
	var searchOutputs = System.Text.Json.JsonDocument.Parse(searchOutputsJson);
	searchServiceName = searchOutputs.RootElement.GetProperty("name").GetProperty("value").GetString() ?? "";
}

// Locate the repository root by walking up from the AppHost directory
// until we find NuGetGallery.sln.
var repoRoot = builder.AppHostDirectory;
while (!File.Exists(Path.Combine(repoRoot, "NuGetGallery.sln")))
{
	var parent = Directory.GetParent(repoRoot)?.FullName
		?? throw new InvalidOperationException(
			$"Could not find NuGetGallery.sln above {builder.AppHostDirectory}");
	repoRoot = parent;
}

var srcDir = Path.Combine(repoRoot, "src");

// ─── Dashboard resource groups ───────────────────────────────────────────────

var infraGroup = builder.AddResource(
	new GroupResource("infrastructure")).ExcludeFromManifest();
var pipelineGroup = builder.AddResource(
	new GroupResource("v3-pipeline")).ExcludeFromManifest();
var searchGroup = builder.AddResource(
	new GroupResource("azure-search")).ExcludeFromManifest();

// ─── Infrastructure ───────────────────────────────────────────────────────────

// Azurite blob storage emulator (hosts all blob containers used by the V3 pipeline)
var storage = builder.AddAzureStorage("storage")
	.RunAsEmulator(r => r
		.WithDataBindMount("./azurite-data")
		.WithBlobPort(10000)
		.WithQueuePort(10001)
		.WithTablePort(10002))
	.WithParentRelationship(infraGroup);
var blobs = storage.AddBlobs("blobs");

var azuriteConnStr = "UseDevelopmentStorage=true";
var azuriteBase = "http://127.0.0.1:10000/devstoreaccount1";

// Azure AI Search — Bicep-provisioned, Basic SKU
var search = builder.AddAzureSearch("search")
	.ConfigureInfrastructure(infra =>
	{
		var searchService = infra.GetProvisionableResources()
			.OfType<Azure.Provisioning.Search.SearchService>()
			.Single();
		searchService.SearchSkuName = Azure.Provisioning.Search.SearchServiceSkuName.Basic;
	})
	.WithParentRelationship(searchGroup);

// ─── Manual reset commands (triggered from Aspire dashboard) ─────────────────

// V3 pipeline containers (cleared on reset). Gallery containers are NOT touched.
var v3Containers = new[]
{
	config.Containers.Catalog, config.Containers.FlatContainer, config.Containers.ServiceIndex,
	config.Containers.RegistrationSemVer1, config.Containers.RegistrationGzSemVer1, config.Containers.RegistrationGzSemVer2,
	config.Containers.AzureSearch, config.Containers.CdnStats, config.Containers.SearchAuxiliary,
};

storage.WithCommand(
	name: "reset-v3-containers",
	displayName: "Reset V3 Containers",
	executeCommand: async context =>
	{
		var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
		var blobService = new Azure.Storage.Blobs.BlobServiceClient("UseDevelopmentStorage=true");
		foreach (var container in v3Containers)
		{
			try
			{
				await blobService.DeleteBlobContainerAsync(container, cancellationToken: context.CancellationToken);
				logger.LogInformation("Deleted container: {Container}", container);
			}
			catch (Azure.RequestFailedException ex) when (ex.Status == 404)
			{
				logger.LogInformation("Container {Container} does not exist, skipping.", container);
			}
		}

		// Re-seed the auxiliary blobs that Db2AzureSearch expects to find on startup.
		logger.LogInformation("V3 containers reset. Restart seed-aux-blobs and jobs to rebuild.");
		return CommandResults.Success();
	},
	commandOptions: new()
	{
		IconName = "Delete",
		IconVariant = IconVariant.Filled,
		IsHighlighted = true,
		ConfirmationMessage = "Delete all V3 pipeline blob containers? Gallery DB and containers (packages, uploads, etc.) will NOT be affected.",
	});

storage.WithCommand(
	name: "delete-all-containers",
	displayName: "Delete All Containers",
	executeCommand: async context =>
	{
		var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
		var blobService = new Azure.Storage.Blobs.BlobServiceClient("UseDevelopmentStorage=true");
		var count = 0;
		await foreach (var container in blobService.GetBlobContainersAsync(
			cancellationToken: context.CancellationToken))
		{
			await blobService.DeleteBlobContainerAsync(container.Name,
				cancellationToken: context.CancellationToken);
			logger.LogInformation("Deleted container: {Container}", container.Name);
			count++;
		}
		logger.LogInformation("Deleted {Count} container(s).", count);
		return CommandResults.Success();
	},
	commandOptions: new()
	{
		IconName = "Delete",
		IconVariant = IconVariant.Filled,
		IsHighlighted = true,
		ConfirmationMessage = "Delete ALL blob containers in Azurite, including Gallery containers (packages, uploads, content, auditing)? This cannot be undone.",
	});

var searchIndexNames = new[] { config.SearchIndexes.Search, config.SearchIndexes.Hijack };

search.WithCommand(
	name: "reset-search-indexes",
	displayName: "Reset Search Indexes",
	executeCommand: async context =>
	{
		var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
		var outputs = context.ServiceProvider.GetRequiredService<IConfiguration>()["Azure:Deployments:search:Outputs"];
		if (string.IsNullOrEmpty(outputs))
		{
			logger.LogWarning("Search deployment outputs not found. Has the search resource been provisioned?");
			return CommandResults.Success();
		}
		var doc = JsonDocument.Parse(outputs);
		var svcName = doc.RootElement.GetProperty("name").GetProperty("value").GetString();
		var endpoint = new Uri($"https://{svcName}.search.windows.net");
		var indexClient = new Azure.Search.Documents.Indexes.SearchIndexClient(
			endpoint, new Azure.Identity.DefaultAzureCredential());
		foreach (var indexName in searchIndexNames)
		{
			try
			{
				await indexClient.DeleteIndexAsync(indexName, context.CancellationToken);
				logger.LogInformation("Deleted search index: {Index}", indexName);
			}
			catch (Azure.RequestFailedException ex) when (ex.Status == 404)
			{
				logger.LogInformation("Search index {Index} does not exist, skipping.", indexName);
			}
		}
		logger.LogInformation("Search indexes reset. Restart db2azuresearch to rebuild.");
		return CommandResults.Success();
	},
	commandOptions: new()
	{
		IconName = "Delete",
		IconVariant = IconVariant.Filled,
		IsHighlighted = true,
		ConfirmationMessage = "Delete all Azure Search indexes (search-index, hijack-index)? They will be recreated by db2azuresearch.",
	});

// ─── DB Initialization ───────────────────────────────────────────────────────

var ef6Exe = Path.Combine(PackagePaths.Ef6ToolsDir, "ef6.exe");
var galleryBin = Path.Combine(srcDir, "NuGetGallery", "bin");
var webConfig = Path.Combine(srcDir, "NuGetGallery", "Web.config");

var dbMigrateGallery = builder.AddExecutable(
	"db-migrate-gallery", ef6Exe, galleryBin,
	"database", "update",
	"--assembly", "NuGetGallery.dll",
	"--migrations-config", "MigrationsConfiguration",
	"--config", webConfig)
	.WithParentRelationship(infraGroup);

dbMigrateGallery.WithCommand(
	name: "drop-gallery-db",
	displayName: "Drop NuGetGallery Database",
	executeCommand: context => DropDatabaseAsync (context, config.GalleryDb.ConnectionString, "NuGetGallery"),
	commandOptions: new()
	{
		IconName = "Delete",
		IconVariant = IconVariant.Filled,
		IsHighlighted = true,
		ConfirmationMessage = "Drop the NuGetGallery database? It will be recreated by restarting this migration.",
	});

var dbMigrateSupport = builder.AddExecutable(
	"db-migrate-support", ef6Exe, galleryBin,
	"database", "update",
	"--assembly", "NuGetGallery.dll",
	"--migrations-config", "SupportRequestMigrationsConfiguration",
	"--config", webConfig)
	.WithParentRelationship(infraGroup);

dbMigrateSupport.WithCommand(
	name: "drop-support-db",
	displayName: "Drop SupportRequest Database",
	executeCommand: context => DropDatabaseAsync (context, config.GalleryDb.ConnectionString, "SupportRequest"),
	commandOptions: new()
	{
		IconName = "Delete",
		IconVariant = IconVariant.Filled,
		IsHighlighted = true,
		ConfirmationMessage = "Drop the SupportRequest database? It will be recreated by restarting this migration.",
	});

// ─── Auxiliary blob seeding (required before Gallery and search jobs) ─────────

var toolsDir = Path.Combine(srcDir, "NuGetGallery.AppHost.Tools");

var seedAuxBlobs = builder.AddExecutable(
	"seed-aux-blobs", "dotnet", toolsDir, "run", "SeedBlobs.cs")
	.WaitFor(storage)
	.WithEnvironment("REPO_ROOT", repoRoot)
	.WithUrl($"{azuriteBase}/{config.Containers.ServiceIndex}/index.json", "V3 Service Index")
	.WithParentRelationship(infraGroup);
WithAppHostEnv(seedAuxBlobs, config, azuriteConnStr, azuriteBase, searchServiceName);

// ─── NuGetGallery web app (IIS Express) ──────────────────────────────────────

var galleryPath = Path.Combine(srcDir, "NuGetGallery");
var iisExpressConfig = Path.Combine(
	repoRoot, ".vs", "NuGetGallery", "config", "applicationhost.config");

// Generate appsettings.Aspire.config to switch Gallery to Azurite blob storage
GenerateGalleryAspireConfig(galleryPath, azuriteConnStr,
	packages: config.Containers.Packages, auditing: config.Containers.Auditing,
	content: config.Containers.Content, uploads: config.Containers.Uploads);

// Polls for the catalog index.json blob in Azurite and exits when it appears.
// Downstream jobs use WaitForCompletion to wait for this, just like the DB migrations.
var catalogIndexUrl = $"{azuriteBase}/{config.Containers.Catalog}/index.json";
var catalogIndexReady = builder.AddExecutable(
	"catalog-index-ready", "dotnet", toolsDir, "run", "V3CatalogIndexAvailable.cs")
	.WaitFor(storage)
	.WithExplicitStart()
	.WithParentRelationship(pipelineGroup);
WithAppHostEnv(catalogIndexReady, config, azuriteConnStr, azuriteBase, searchServiceName);

var gallery = builder.AddExecutable(
	"gallery",
	@"C:\Program Files\IIS Express\iisexpress.exe",
	galleryPath,
	"/config:" + iisExpressConfig,
	"/site:NuGet Gallery (localhost)")
	.WithHttpEndpoint(port: 80, name: "gallery-http", isProxied: false)
	.WithHttpsEndpoint(port: 443, name: "gallery-https", isProxied: false)
    .WaitForCompletion(dbMigrateGallery)
    .WaitForCompletion(dbMigrateSupport)
	// .WaitForCompletion(seedAuxBlobs)
	.WaitFor(storage);

// ─── Ng sub-commands (CLI args) ──────────────────────────────────────────────

var db2catalog = builder.AddProject<Projects.Ng>("db2catalog")
	.WithArgs("db2catalog",
		"-gallery",             config.GalleryBaseAddress,
		"-storageType",         "azure",
		"-storageConnectionString", azuriteConnStr,
		"-storageBaseAddress",  $"{azuriteBase}/{config.Containers.Catalog}/",
		"-storageContainer",    config.Containers.Catalog,
		"-storageInitializeContainer", "true",
		"-connectionString",    config.GalleryDb.ConnectionString,
		"-packageContentUrlFormat", $"{azuriteBase}/{config.Containers.Packages}/{{id-lower}}.{{version-lower}}.nupkg",
		"-cursorSize",          config.Settings.CursorSize.ToString(),
		"-preferAlternatePackageSourceStorage", "true",
		"-storageConnectionStringPreferredPackageSourceStorage", azuriteConnStr,
		"-storageBaseAddressPreferredPackageSourceStorage", $"{azuriteBase}/{config.Containers.Packages}/",
		"-storageContainerPreferredPackageSourceStorage", config.Containers.Packages,
		"-storageTypeAuditing",        "azure",
		"-storageConnectionStringAuditing", azuriteConnStr,
		"-storageContainerAuditing",   config.Containers.Auditing,
		"-storagePathAuditing",        "package",
		"-verbose",             "true",
		"-interval",            config.Settings.PollIntervalSeconds.ToString())
    .WaitForCompletion(dbMigrateGallery)
    .WaitFor(storage)
	.WithUrl($"{azuriteBase}/{config.Containers.Catalog}/index.json", "Catalog Index")
	.WithExplicitStart()
	.WithParentRelationship(pipelineGroup);

builder.AddProject<Projects.Ng>("catalog2dnx")
	.WithArgs("catalog2dnx",
		"-source",              catalogIndexUrl,
		"-storageType",         "azure",
		"-storageConnectionString", azuriteConnStr,
		"-storageBaseAddress",  $"{azuriteBase}/{config.Containers.FlatContainer}/",
		"-storageContainer",    config.Containers.FlatContainer,
		"-contentBaseAddress",  $"{azuriteBase}/{config.Containers.Packages}/",
		"-preferAlternatePackageSourceStorage", "true",
		"-storageConnectionStringPreferredPackageSourceStorage", azuriteConnStr,
		"-storageBaseAddressPreferredPackageSourceStorage", $"{azuriteBase}/{config.Containers.Packages}/",
		"-storageContainerPreferredPackageSourceStorage", config.Containers.Packages,
		"-verbose",             "true",
		"-interval",            config.Settings.PollIntervalSeconds.ToString())
	.WaitForCompletion(catalogIndexReady)
	.WithExplicitStart()
	.WithParentRelationship(pipelineGroup);

// ─── Standalone jobs (JsonConfigurationJob — each gets its own config file) ──

var catalog2regConfigPath = GenerateJsonConfig(
	builder.AppHostDirectory, "catalog2registration-dev.json", new
	{
		GalleryDb = new { ConnectionString = config.GalleryDb.ConnectionString },
		Catalog2Registration = new
		{
			Source = catalogIndexUrl,
			StorageConnectionString = azuriteConnStr,
			LegacyStorageContainer = config.Containers.RegistrationSemVer1,
			LegacyBaseUrl = $"{azuriteBase}/{config.Containers.RegistrationSemVer1}/",
			GzippedStorageContainer = config.Containers.RegistrationGzSemVer1,
			GzippedBaseUrl = $"{azuriteBase}/{config.Containers.RegistrationGzSemVer1}/",
			SemVer2StorageContainer = config.Containers.RegistrationGzSemVer2,
			SemVer2BaseUrl = $"{azuriteBase}/{config.Containers.RegistrationGzSemVer2}/",
			CreateContainers = true,
			FlatContainerBaseUrl = $"{azuriteBase}/{config.Containers.FlatContainer}/",
			GalleryBaseUrl = config.GalleryBaseAddress,
		},
	});

builder.AddProject<Projects.NuGet_Jobs_Catalog2Registration>("catalog2registration")
	.WithArgs("-Configuration", catalog2regConfigPath)
	.WaitForCompletion(catalogIndexReady)
	.WithExplicitStart()
	.WithParentRelationship(pipelineGroup);

// Polls Azure Search for indexes + cursor.json in Azurite created by db2azuresearch.
// Decouples dependents from db2azuresearch's exit code — if indexes already exist from a
// prior run, dependents start immediately even when db2azuresearch fails on "already exists".
var searchIndexReady = builder.AddExecutable(
	"search-index-ready", "dotnet", toolsDir, "run", "SearchIndexAvailable.cs")
	.WaitFor(search)
	.WithExplicitStart()
	.WithParentRelationship(searchGroup);
WithAppHostEnv(searchIndexReady, config, azuriteConnStr, azuriteBase, searchServiceName);

var catalog2searchConfigPath = GenerateJsonConfig(
	builder.AppHostDirectory, "catalog2azuresearch-dev.json", new
	{
		GalleryDb = new { ConnectionString = config.GalleryDb.ConnectionString },
		Catalog2AzureSearch = new
		{
			Source = catalogIndexUrl,
			StorageConnectionString = azuriteConnStr,
			StorageContainer = config.Containers.AzureSearch,
			CreateContainersAndIndexes = false,
			RegistrationsBaseUrl = $"{azuriteBase}/{config.Containers.RegistrationGzSemVer2}/",
			FlatContainerBaseUrl = $"{azuriteBase}/{config.Containers.FlatContainer}/",
			FlatContainerContainerName = config.Containers.FlatContainer,
			SearchServiceName = searchServiceName,
			SearchServiceUseDefaultCredential = true,
			SearchIndexName = config.SearchIndexes.Search,
			HijackIndexName = config.SearchIndexes.Hijack,
			GalleryBaseUrl = config.GalleryBaseAddress,
		},
	});

// catalog2azuresearch resource is declared after db2azuresearch (dependency below).

// ─── Db2AzureSearch (one-shot initial seed of search indexes) ────────────────

var downloadsV1JsonUrl = $"{azuriteBase}/{config.Containers.CdnStats}/{config.AuxiliaryBlobs.DownloadsV1Json}";

var db2searchConfigPath = GenerateJsonConfig(
	builder.AppHostDirectory, "db2azuresearch-dev.json", new
	{
		GalleryDb = new { ConnectionString = config.GalleryDb.ConnectionString },
		Db2AzureSearch = new
		{
			SearchServiceName = searchServiceName,
			SearchServiceUseDefaultCredential = true,
			SearchIndexName = config.SearchIndexes.Search,
			HijackIndexName = config.SearchIndexes.Hijack,
			StorageConnectionString = azuriteConnStr,
			StorageContainer = config.Containers.AzureSearch,
			GalleryBaseUrl = config.GalleryBaseAddress,
			CatalogIndexUrl = catalogIndexUrl,
			DownloadsV1JsonUrl = downloadsV1JsonUrl,
			DownloadsV1JsonConnectionString = azuriteConnStr,
			AuxiliaryDataStorageConnectionString = azuriteConnStr,
			AuxiliaryDataStorageContainer = config.Containers.SearchAuxiliary,
			AuxiliaryDataStorageExcludedPackagesPath = config.AuxiliaryBlobs.ExcludedPackagesJson,
			FlatContainerBaseUrl = $"{azuriteBase}/{config.Containers.FlatContainer}/",
			FlatContainerContainerName = config.Containers.FlatContainer,
			AllIconsInFlatContainer = true,
			EnablePopularityTransfers = config.Settings.EnablePopularityTransfers,
			Scoring = config.Scoring,
			Development = new
			{
				ReplaceContainersAndIndexes = false,
			},
		},
	});
    
var db2azuresearch = builder.AddProject<Projects.NuGet_Jobs_Db2AzureSearch>("db2azuresearch")
	.WithArgs("-Configuration", db2searchConfigPath, "-Once", "true")
	.WaitForCompletion(catalogIndexReady)
	.WaitForCompletion(seedAuxBlobs)
    .WaitForCompletion(dbMigrateGallery)
    .WaitFor(search)
	.WithExplicitStart()
	.WithParentRelationship(searchGroup);

builder.AddProject<Projects.NuGet_Jobs_Catalog2AzureSearch>("catalog2azuresearch")
	.WithArgs("-Configuration", catalog2searchConfigPath)
	.WaitForCompletion(catalogIndexReady)
	.WaitForCompletion(searchIndexReady)
	.WaitFor(search)
	.WithExplicitStart()
	.WithParentRelationship(searchGroup);

// ─── Auxiliary2AzureSearch (ongoing updates to auxiliary blobs) ───────────────

var auxiliary2searchConfigPath = GenerateJsonConfig(
	builder.AppHostDirectory, "auxiliary2azuresearch-dev.json", new
	{
		GalleryDb = new { ConnectionString = config.GalleryDb.ConnectionString },
		Auxiliary2AzureSearch = new
		{
			SearchServiceName = searchServiceName,
			SearchServiceUseDefaultCredential = true,
			SearchIndexName = config.SearchIndexes.Search,
			HijackIndexName = config.SearchIndexes.Hijack,
			StorageConnectionString = azuriteConnStr,
			StorageContainer = config.Containers.AzureSearch,
			DownloadsV1JsonUrl = downloadsV1JsonUrl,
			DownloadsV1JsonConnectionString = azuriteConnStr,
			MinPushPeriod = config.Settings.MinPushPeriod,
			MaxDownloadCountDecreases = config.Settings.MaxDownloadCountDecreases,
			EnablePopularityTransfers = config.Settings.EnablePopularityTransfers,
			Scoring = new
			{
				PopularityTransfer = config.Scoring.PopularityTransfer,
			},
		},
	});

builder.AddProject<Projects.NuGet_Jobs_Auxiliary2AzureSearch>("auxiliary2azuresearch")
	.WithArgs("-Configuration", auxiliary2searchConfigPath)
	.WaitForCompletion(searchIndexReady)
	.WaitForCompletion(seedAuxBlobs)
	.WaitFor(search)
	.WithExplicitStart()
	.WithParentRelationship(searchGroup);

// ─── Search Service (ASP.NET Core web app) ───────────────────────────────────

var searchServiceUri = new Uri(config.SearchServiceBaseAddress);

builder.AddProject<Projects.NuGet_Services_SearchService_Core>("search-service")
	.WithHttpEndpoint(port: searchServiceUri.Port, name: "search-http", isProxied: false)
	.WithEnvironment("ASPNETCORE_URLS", config.SearchServiceBaseAddress)
	.WithEnvironment("APPSETTING_SearchService__SearchServiceName", searchServiceName)
	.WithEnvironment("APPSETTING_SearchService__SearchServiceApiKey", "")
	.WithEnvironment("APPSETTING_SearchService__SearchServiceUseDefaultCredential", "true")
	.WithEnvironment("APPSETTING_SearchService__SearchIndexName", config.SearchIndexes.Search)
	.WithEnvironment("APPSETTING_SearchService__HijackIndexName", config.SearchIndexes.Hijack)
	.WithEnvironment("APPSETTING_SearchService__StorageConnectionString", azuriteConnStr)
	.WithEnvironment("APPSETTING_SearchService__StorageContainer", config.Containers.AzureSearch)
	.WithEnvironment("APPSETTING_SearchService__FlatContainerBaseUrl", $"{azuriteBase}/{config.Containers.FlatContainer}/")
	.WithEnvironment("APPSETTING_SearchService__FlatContainerContainerName", config.Containers.FlatContainer)
	.WithEnvironment("APPSETTING_SearchService__AllIconsInFlatContainer", "true")
	.WithEnvironment("APPSETTING_SearchService__SemVer1RegistrationsBaseUrl", $"{azuriteBase}/{config.Containers.RegistrationSemVer1}/")
	.WithEnvironment("APPSETTING_SearchService__SemVer2RegistrationsBaseUrl", $"{azuriteBase}/{config.Containers.RegistrationGzSemVer2}/")
	.WithEnvironment("APPSETTING_FeatureFlags__ConnectionString", azuriteConnStr)
	.WaitForCompletion(searchIndexReady)
	.WaitFor(search)
	.WaitFor(storage)
	.WithExplicitStart()
	.WithParentRelationship(searchGroup);

// ─── Group "Start All" commands ──────────────────────────────────────────────

var pipelineResources = new[] { "catalog-index-ready", "db2catalog", "catalog2dnx", "catalog2registration" };
var searchResources = new[] { "search-index-ready", "db2azuresearch", "catalog2azuresearch", "auxiliary2azuresearch", "search-service" };

pipelineGroup.WithCommand(
	name: "start-all",
	displayName: "Start V3 Pipeline",
	executeCommand: context => StartGroupAsync (context, pipelineResources),
	commandOptions: new()
	{
		IconName = "Play",
		IconVariant = IconVariant.Filled,
	});

searchGroup.WithCommand(
	name: "start-all",
	displayName: "Start Azure Search",
	executeCommand: context => StartGroupAsync (context, searchResources),
	commandOptions: new()
	{
		IconName = "Play",
		IconVariant = IconVariant.Filled,
	});

builder.Build().Run();

// ─── Helpers ─────────────────────────────────────────────────────────────────

/// <summary>
/// Sets all NuGetGalleryConfig properties as environment variables on an executable resource,
/// using the __ separator convention so IConfiguration binds them back to the POCO.
/// </summary>
static void WithAppHostEnv<T> (
	IResourceBuilder<T> builder,
	NuGetGalleryConfig config, string storageConnStr, string azuriteBase, string searchServiceName)
	where T : IResourceWithEnvironment
{
	builder
		.WithEnvironment("GalleryBaseAddress", config.GalleryBaseAddress)
		.WithEnvironment("StorageConnectionString", storageConnStr)
		.WithEnvironment("AzuriteBase", azuriteBase)
		.WithEnvironment("SearchServiceName", searchServiceName)
		.WithEnvironment("Containers__Catalog", config.Containers.Catalog)
		.WithEnvironment("Containers__FlatContainer", config.Containers.FlatContainer)
		.WithEnvironment("Containers__RegistrationSemVer1", config.Containers.RegistrationSemVer1)
		.WithEnvironment("Containers__RegistrationGzSemVer1", config.Containers.RegistrationGzSemVer1)
		.WithEnvironment("Containers__RegistrationGzSemVer2", config.Containers.RegistrationGzSemVer2)
		.WithEnvironment("Containers__AzureSearch", config.Containers.AzureSearch)
		.WithEnvironment("Containers__ServiceIndex", config.Containers.ServiceIndex)
		.WithEnvironment("Containers__CdnStats", config.Containers.CdnStats)
		.WithEnvironment("Containers__SearchAuxiliary", config.Containers.SearchAuxiliary)
		.WithEnvironment("Containers__Packages", config.Containers.Packages)
		.WithEnvironment("Containers__Content", config.Containers.Content)
		.WithEnvironment("AuxiliaryBlobs__DownloadsV1Json", config.AuxiliaryBlobs.DownloadsV1Json)
		.WithEnvironment("AuxiliaryBlobs__ExcludedPackagesJson", config.AuxiliaryBlobs.ExcludedPackagesJson)
		.WithEnvironment("AuxiliaryBlobs__FlagsJson", config.AuxiliaryBlobs.FlagsJson)
		.WithEnvironment("SearchIndexes__Search", config.SearchIndexes.Search)
		.WithEnvironment("SearchIndexes__Hijack", config.SearchIndexes.Hijack)
		.WithEnvironment("SearchServiceBaseAddress", config.SearchServiceBaseAddress);
}

static async Task<ExecuteCommandResult> StartGroupAsync (
	ExecuteCommandContext context, string[] resourceNames)
{
	var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
	var commandService = context.ServiceProvider.GetRequiredService<ResourceCommandService>();
	foreach (var name in resourceNames)
	{
		logger.LogInformation("Starting resource: {Resource}", name);
		await commandService.ExecuteCommandAsync(name, "resource-start", context.CancellationToken);
	}
	return CommandResults.Success();
}

static string GenerateJsonConfig (string appHostDir, string fileName, object content)
{
	var path = Path.Combine(appHostDir, fileName);
	File.WriteAllText(path, JsonSerializer.Serialize(content,
		new JsonSerializerOptions { WriteIndented = true }));
	return path;
}

/// <summary>
/// Writes appsettings.Aspire.config into the Gallery project directory.
/// This file is loaded by Web.config's &lt;appSettings file="..."&gt; attribute
/// and switches Gallery from FileSystem storage to Azurite blob storage.
/// </summary>
static void GenerateGalleryAspireConfig (
	string galleryDir, string connectionString,
	string packages, string auditing, string content, string uploads)
{
	var doc = new XDocument(
		new XElement("appSettings",
			Setting("Gallery.StorageType", "AzureStorage"),
			Setting("Gallery.AzureStorage.Auditing.ConnectionString", connectionString),
			Setting("Gallery.AzureStorage.Auditing.ContainerName", auditing),
			Setting("Gallery.AzureStorage.UserCertificates.ConnectionString", connectionString),
			Setting("Gallery.AzureStorage.Content.ConnectionString", connectionString),
			Setting("Gallery.AzureStorage.Content.ContainerName", content),
			Setting("Gallery.AzureStorage.Errors.ConnectionString", connectionString),
			Setting("Gallery.AzureStorage.Packages.ConnectionString", connectionString),
			Setting("Gallery.AzureStorage.Packages.ContainerName", packages),
			Setting("Gallery.AzureStorage.FlatContainer.ConnectionString", connectionString),
			Setting("Gallery.AzureStorage.Statistics.ConnectionString", connectionString),
			Setting("Gallery.AzureStorage.Statistics.ConnectionString.Alternate", connectionString),
			Setting("Gallery.AzureStorage.Uploads.ConnectionString", connectionString),
			Setting("Gallery.AzureStorage.Uploads.ContainerName", uploads),
			Setting("Gallery.AzureStorage.Revalidation.ConnectionString", connectionString)));

	doc.Save(Path.Combine(galleryDir, "appsettings.Aspire.config"));

	static XElement Setting (string key, string value) =>
		new("add", new XAttribute("key", key), new XAttribute("value", value));
}

static async Task<ExecuteCommandResult> DropDatabaseAsync (
	ExecuteCommandContext context, string connectionString, string databaseName)
{
	var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
	var connStr = connectionString;
	if (string.IsNullOrEmpty(connStr))
	{
		throw new InvalidOperationException(
			$"Cannot drop database '{databaseName}': connection string is not configured.");
	}

	var masterConnStr = new System.Data.SqlClient.SqlConnectionStringBuilder(connStr)
	{
		InitialCatalog = "master"
	}.ConnectionString;

	using var conn = new System.Data.SqlClient.SqlConnection(masterConnStr);
	await conn.OpenAsync(context.CancellationToken);
	try
	{
		using var cmd = conn.CreateCommand();
		cmd.CommandText = $"""
			IF DB_ID('{databaseName}') IS NOT NULL
			BEGIN
				ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
				DROP DATABASE [{databaseName}];
			END
			""";
		await cmd.ExecuteNonQueryAsync(context.CancellationToken);
		logger.LogInformation("Dropped database: {Database}", databaseName);
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "Failed to drop database: {Database}", databaseName);
	}
	return CommandResults.Success();
}

/// <summary>Lightweight resource used purely for visual grouping in the Aspire dashboard.</summary>
sealed class GroupResource (string name) : Resource(name);

