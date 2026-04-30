// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Aspire.Hosting.ApplicationModel;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = DistributedApplication.CreateBuilder(args);
		var config = builder.Configuration.GetSection("NuGetGallery").Get<NuGetGalleryConfig>()!;

		// APPHOST_PROFILE controls which resources are launched.
		// "ci-gallery" = minimal: only Azurite, DB migrations, and Gallery.
		var profile = Environment.GetEnvironmentVariable("APPHOST_PROFILE") ?? "full";

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

		// ─── Infrastructure ───────────────────────────────────────────────────────────

		// Azurite blob storage emulator (hosts all blob containers used by the V3 pipeline).
		// Priority: VS-bundled exe → npx azurite → Docker container.
		var storage = builder.AddLocalOrEmulatorAzurite(dataPath: "./azurite-data");
		builder.CreateResourceBuilder(storage.Resource)
			.WithParentRelationship(infraGroup);

		var azuriteConnStr = "UseDevelopmentStorage=true";
		var azuriteBase = "http://127.0.0.1:10000/devstoreaccount1";

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
			executeCommand: context => DropDatabaseAsync(context, config.GalleryDb.ConnectionString, "NuGetGallery"),
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
			executeCommand: context => DropDatabaseAsync(context, config.GalleryDb.ConnectionString, "SupportRequest"),
			commandOptions: new()
			{
				IconName = "Delete",
				IconVariant = IconVariant.Filled,
				IsHighlighted = true,
				ConfirmationMessage = "Drop the SupportRequest database? It will be recreated by restarting this migration.",
			});

		// ─── NuGetGallery web app (IIS Express) ──────────────────────────────────────

		var galleryPath = Path.Combine(srcDir, "NuGetGallery");
		var iisUserHome = Path.Combine(repoRoot, ".vs");
		var iisExpressConfig = Path.Combine(iisUserHome, "config", "applicationhost.config");

		// IIS Express needs absolute physicalPath when launched via Aspire/DCP.
		// The checked-in config uses a relative path that works from VS but not
		// when the process working directory is set by DCP.
		EnsureAbsolutePhysicalPath(iisExpressConfig, galleryPath);

		// Ensure IIS Express user home directories and aspnet.config exist.
		// On CI agents these may not be present; on dev machines they already are.
		EnsureIISExpressUserHome(iisUserHome);

		// Generate appsettings.Aspire.config to switch Gallery to Azurite blob storage
		GenerateGalleryAspireConfig(galleryPath, azuriteConnStr,
			packages: config.Containers.Packages, auditing: config.Containers.Auditing,
			content: config.Containers.Content, uploads: config.Containers.Uploads);

		var gallery = builder.AddExecutable(
			"gallery",
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe"),
			galleryPath,
			"/config:" + iisExpressConfig,
			"/site:NuGet Gallery (localhost)",
			"/userhome:" + iisUserHome)
			.WithHttpEndpoint(port: 80, name: "gallery-http", isProxied: false)
			.WithHttpsEndpoint(port: 443, name: "gallery-https", isProxied: false)
		    .WaitForCompletion(dbMigrateGallery)
		    .WaitForCompletion(dbMigrateSupport)
			.WaitFor(storage)
			.WithEnvironment("IIS_USER_HOME", iisUserHome);

		// ─── Full profile: V3 pipeline, seeding, search, and dashboard commands ──────
		// Azure AI Search, V3 pipeline resources, seeding, and dashboard commands
		// are only needed in the full profile.
		if (profile != "ci-gallery")
		{

		// Azure AI Search — Bicep-provisioned, Basic SKU
		var search = builder.AddAzureSearch("search")
			.ConfigureInfrastructure(infra =>
			{
				var searchService = infra.GetProvisionableResources()
					.OfType<Azure.Provisioning.Search.SearchService>()
					.Single();
				searchService.SearchSkuName = Azure.Provisioning.Search.SearchServiceSkuName.Basic;
			})
			.WithParentRelationship(pipelineGroup);

		// ─── Reset arrays (triggered from Aspire dashboard commands) ─────────────────

		// Containers produced by V3 jobs only (not seeded data) — used by "Stop and Reset V3".
		var jobContainers = new[]
		{
			config.Containers.Catalog, config.Containers.FlatContainer,
			config.Containers.RegistrationSemVer1, config.Containers.RegistrationGzSemVer1, config.Containers.RegistrationGzSemVer2,
			config.Containers.AzureSearch,
		};

		var searchIndexNames = new[] { config.SearchIndexes.Search, config.SearchIndexes.Hijack };

		// ─── Auxiliary blob seeding (required before Gallery and search jobs) ─────────

		var seedBlobs = builder.AddProject<Projects.NuGetGallery_AppHost_Tools>("seed-blobs")
			.WithArgs("seed-blobs")
			.WaitFor(storage)
			.WithEnvironment("REPO_ROOT", repoRoot)
			.WithUrl($"{azuriteBase}/{config.Containers.ServiceIndex}/index.json", "V3 Service Index")
			.WithParentRelationship(infraGroup);
		WithAppHostEnv(seedBlobs, config, azuriteConnStr, azuriteBase, searchServiceName);

		// Generate appsettings.Aspire.config for GalleryTools so it talks to Azurite + the right SQL DB
		var galleryToolsBin = Path.Combine(srcDir, "GalleryTools", "bin",
		#if DEBUG
			"Debug",
		#else
			"Release",
		#endif
			"net472");
		GenerateGalleryToolsConfig(galleryToolsBin, azuriteConnStr,
			config.GalleryDb.ConnectionString, config.GalleryBaseAddress,
			packages: config.Containers.Packages, auditing: config.Containers.Auditing,
			content: config.Containers.Content, uploads: config.Containers.Uploads);

		// Polls for the catalog index.json blob in Azurite and exits when it appears.
		// Downstream jobs use WaitForCompletion to wait for this, just like the DB migrations.
		var catalogIndexUrl = $"{azuriteBase}/{config.Containers.Catalog}/index.json";
		var catalogIndexReady = builder.AddProject<Projects.NuGetGallery_AppHost_Tools>("catalog-index-ready")
			.WithArgs("catalog-index-available")
			.WaitFor(storage)
			.WithParentRelationship(pipelineGroup);
		WithAppHostEnv(catalogIndexReady, config, azuriteConnStr, azuriteBase, searchServiceName);

		builder.AddProject<Projects.NuGetGallery_AppHost_Tools>("warmup-gallery")
			.WithArgs("warmup")
			.WithEnvironment("WarmupBaseUrl", config.GalleryBaseAddress)
			.WithEnvironment("WarmupPaths", "/,/packages,/api/v2")
			.WaitFor(gallery)
			.WithParentRelationship(infraGroup);

		// ─── Test data seeding (creates a user and pushes a test package) ────────────

		var galleryToolsExe = Path.Combine(galleryToolsBin, "GalleryTools.exe");
		var testNupkg = Path.Combine(builder.AppHostDirectory, "testdata", "basetestpackage.1.0.0.nupkg.testdata");

		var seedUser = builder.AddExecutable(
			"seed-user", galleryToolsExe, galleryToolsBin,
			"createuser",
			"--username", "NuGetTestData",
			"--password", "Password1!",
			"--email", "NuGetTestData@localhost")
			.WaitForCompletion(dbMigrateGallery)
			.WaitFor(storage)
			.WithParentRelationship(infraGroup);

		var seedPackage = builder.AddExecutable(
			"seed-package", galleryToolsExe, galleryToolsBin,
			"pushpackage",
			"--owner", "NuGetTestData",
			"--package", testNupkg)
			.WaitForCompletion(seedUser)
			.WaitFor(storage)
			.WithParentRelationship(infraGroup);

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
			.WithParentRelationship(pipelineGroup);

		// ─── Standalone jobs(JsonConfigurationJob — each gets its own config file) ──

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
			.WithParentRelationship(pipelineGroup);

		// Polls Azure Searchfor indexes + cursor.json in Azurite created by db2azuresearch.
		// Decouples dependents from db2azuresearch's exit code — if indexes already exist from a
		// prior run, dependents start immediately even when db2azuresearch fails on "already exists".
		var searchIndexReady = builder.AddProject<Projects.NuGetGallery_AppHost_Tools>("search-index-ready")
			.WithArgs("search-index-available")
			.WaitFor(search)
			.WithParentRelationship(pipelineGroup);
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
			.WaitForCompletion(seedBlobs)
		    .WaitForCompletion(dbMigrateGallery)
		    .WaitFor(search)
			.WithParentRelationship(pipelineGroup);

		builder.AddProject<Projects.NuGet_Jobs_Catalog2AzureSearch>("catalog2azuresearch")
			.WithArgs("-Configuration", catalog2searchConfigPath)
			.WaitForCompletion(catalogIndexReady)
			.WaitForCompletion(searchIndexReady)
			.WaitFor(search)
			.WithParentRelationship(pipelineGroup);

		// ─── Auxiliary2AzureSearch(ongoing updates to auxiliary blobs) ───────────────

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
			.WaitForCompletion(seedBlobs)
			.WaitFor(search)
			.WithParentRelationship(pipelineGroup);

		// ─── Search Service(ASP.NET Core web app) ───────────────────────────────────

		var searchServiceUri = new Uri(config.SearchServiceBaseAddress);

		var searchService = builder.AddProject<Projects.NuGet_Services_SearchService_Core>("search-service")
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
			.WithParentRelationship(pipelineGroup);

		builder.AddProject<Projects.NuGetGallery_AppHost_Tools>("warmup-search")
			.WithArgs("warmup")
			.WithEnvironment("WarmupBaseUrl", config.SearchServiceBaseAddress)
			.WithEnvironment("WarmupPaths", "/search/diag,/query")
			.WaitFor(searchService)
			.WithParentRelationship(pipelineGroup);

		// ─── Group "Start All" / "Stop and Reset" commands ──────────────────────────

		var allV3Resources = new[]
		{
			"catalog-index-ready", "db2catalog", "catalog2dnx", "catalog2registration",
			"search-index-ready", "db2azuresearch", "catalog2azuresearch", "auxiliary2azuresearch", "search-service",
		};

		pipelineGroup.WithCommand(
			name: "stop-and-reset",
			displayName: "Stop and Reset V3",
			executeCommand: async context =>
			{
				var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
				var commandService = context.ServiceProvider.GetRequiredService<ResourceCommandService>();

				// 1. Stop all V3 resources (ignore errors for already-stopped resources)
				foreach (var name in allV3Resources)
				{
					try
					{
						logger.LogInformation("Stopping {Resource}...", name);
						await commandService.ExecuteCommandAsync(name, "resource-stop", context.CancellationToken);
					}
					catch
					{
						logger.LogInformation("{Resource} was not running, skipping.", name);
					}
				}

				// 2. Delete job-produced blob containers (catalog, flatcontainer, registrations, azuresearch).
				// Seeded containers (service-index, cdn-stats, search-auxiliary) are preserved.
				var blobService = new Azure.Storage.Blobs.BlobServiceClient("UseDevelopmentStorage=true");
				foreach (var container in jobContainers)
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

				// 3. Delete Azure Search indexes
				var outputs = context.ServiceProvider.GetRequiredService<IConfiguration>()["Azure:Deployments:search:Outputs"];
				if (!string.IsNullOrEmpty(outputs))
				{
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
				}
				else
				{
					logger.LogWarning("Search deployment outputs not found — search indexes were not deleted.");
				}

				logger.LogInformation("V3 reset complete. Resources will auto-start when Aspire is restarted.");
				return CommandResults.Success();
			},
			commandOptions: new()
			{
				IconName = "Delete",
				IconVariant = IconVariant.Filled,
				IsHighlighted = true,
				ConfirmationMessage = "Stop all V3 pipeline jobs, delete job-produced blob containers, and delete Azure Search indexes? " +
					"The Gallery DB, Gallery blobs, and seeded auxiliary data will NOT be affected.",
			});

		// ─── "Stop and Reset Everything" on infrastructure group ─────────────────────

		infraGroup.WithCommand(
			name: "stop-and-reset-everything",
			displayName: "Stop and Reset Everything",
			executeCommand: async context =>
			{
				var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
				var commandService = context.ServiceProvider.GetRequiredService<ResourceCommandService>();

				// 1. Stop all V3 resources + Gallery
				var allStoppable = allV3Resources.Concat(new[] { "gallery" }).ToArray();
				foreach (var name in allStoppable)
				{
					try
					{
						logger.LogInformation("Stopping {Resource}...", name);
						await commandService.ExecuteCommandAsync(name, "resource-stop", context.CancellationToken);
					}
					catch
					{
						logger.LogInformation("{Resource} was not running, skipping.", name);
					}
				}

				// 2. Drop databases
				await DropDatabaseAsync(context, config.GalleryDb.ConnectionString, "NuGetGallery");
				await DropDatabaseAsync(context, config.GalleryDb.ConnectionString, "SupportRequest");
				logger.LogInformation("Databases dropped.");

				// 3. Delete ALL blob containers
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

				// 4. Delete Azure Search indexes
				var outputs = context.ServiceProvider.GetRequiredService<IConfiguration>()["Azure:Deployments:search:Outputs"];
				if (!string.IsNullOrEmpty(outputs))
				{
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
				}
				else
				{
					logger.LogWarning("Search deployment outputs not found — search indexes were not deleted.");
				}

				logger.LogInformation("Full reset complete. Restart Aspire to rebuild everything from scratch.");
				return CommandResults.Success();
			},
			commandOptions: new()
			{
				IconName = "Delete",
				IconVariant = IconVariant.Filled,
				IsHighlighted = true,
				ConfirmationMessage = "Stop everything, drop ALL databases, delete ALL blob containers, and delete ALL search indexes? " +
					"This will completely reset the local environment. Restart Aspire to rebuild from scratch.",
			});

		} // end if (profile != "ci-gallery")

		builder.Build().Run();
	}

	// ─── Helpers ─────────────────────────────────────────────────────────────────


	/// <summary>
	/// Sets all NuGetGalleryConfig properties as environment variables on an executable resource,
	/// using the __ separator convention so IConfiguration binds them back to the POCO.
	/// </summary>
	static void WithAppHostEnv<T>(
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

	static string GenerateJsonConfig(string appHostDir, string fileName, object content)
	{
		var path = Path.Combine(appHostDir, fileName);
		File.WriteAllText(path, JsonSerializer.Serialize(content,
			new JsonSerializerOptions { WriteIndented = true }));
		return path;
	}

	/// <summary>
	/// Ensures the IIS Express site's physicalPath is absolute. The checked-in
	/// applicationhost.config uses a relative path that works when VS launches
	/// IIS Express but fails when Aspire/DCP sets a different working directory.
	/// </summary>
	static void EnsureAbsolutePhysicalPath(string configPath, string galleryPath)
	{
		var content = File.ReadAllText(configPath);
		var absPath = Path.GetFullPath(galleryPath);

		const string relativePhysicalPath = @"physicalPath=""..\..\src\NuGetGallery""";
		if (content.Contains(relativePhysicalPath))
		{
			content = content.Replace(relativePhysicalPath, $@"physicalPath=""{absPath}""");
			File.WriteAllText(configPath, content);
		}
	}

	/// <summary>
	/// Creates IIS Express user home directories and copies aspnet.config from
	/// the IIS Express templates if it doesn't already exist.
	/// </summary>
	static void EnsureIISExpressUserHome(string iisUserHome)
	{
		foreach (var subdir in new[] { "config", "Logs", "TraceLogFiles" })
		{
			Directory.CreateDirectory(Path.Combine(iisUserHome, subdir));
		}

		var aspnetConfigPath = Path.Combine(iisUserHome, "config", "aspnet.config");
		if (!File.Exists(aspnetConfigPath))
		{
			var templatePath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				"IIS Express", "config", "templates", "PersonalWebServer", "aspnet.config");
			if (File.Exists(templatePath))
			{
				File.Copy(templatePath, aspnetConfigPath);
			}
		}
	}

	/// <summary>
	/// Writes appsettings.Aspire.config into the Gallery project directory.
	/// This file is loaded by Web.config's &lt;appSettings file="..."&gt; attribute
	/// and switches Gallery from FileSystem storage to Azurite blob storage.
	/// </summary>
	static void GenerateGalleryAspireConfig(
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

		static XElement Setting(string key, string value) =>
			new("add", new XAttribute("key", key), new XAttribute("value", value));
	}

	/// <summary>
	/// Writes appsettings.Aspire.config into the GalleryTools output directory.
	/// GalleryTools' App.config has &lt;appSettings file="appsettings.Aspire.config"&gt;
	/// which loads these settings as overrides, switching from FileSystem to Azurite storage.
	/// </summary>
	static void GenerateGalleryToolsConfig(
		string toolsBinDir, string storageConnectionString, string sqlConnectionString,
		string siteRoot, string packages, string auditing, string content, string uploads)
	{
		Directory.CreateDirectory(toolsBinDir);
		var doc = new XDocument(
			new XElement("appSettings",
				Setting("Gallery.StorageType", "AzureStorage"),
				Setting("Gallery.AzureStorage.Auditing.ConnectionString", storageConnectionString),
				Setting("Gallery.AzureStorage.Auditing.ContainerName", auditing),
				Setting("Gallery.AzureStorage.UserCertificates.ConnectionString", storageConnectionString),
				Setting("Gallery.AzureStorage.Content.ConnectionString", storageConnectionString),
				Setting("Gallery.AzureStorage.Content.ContainerName", content),
				Setting("Gallery.AzureStorage.Errors.ConnectionString", storageConnectionString),
				Setting("Gallery.AzureStorage.Packages.ConnectionString", storageConnectionString),
				Setting("Gallery.AzureStorage.Packages.ContainerName", packages),
				Setting("Gallery.AzureStorage.FlatContainer.ConnectionString", storageConnectionString),
				Setting("Gallery.AzureStorage.Statistics.ConnectionString", storageConnectionString),
				Setting("Gallery.AzureStorage.Statistics.ConnectionString.Alternate", storageConnectionString),
				Setting("Gallery.AzureStorage.Uploads.ConnectionString", storageConnectionString),
				Setting("Gallery.AzureStorage.Uploads.ContainerName", uploads),
				Setting("Gallery.AzureStorage.Revalidation.ConnectionString", storageConnectionString),
				Setting("Gallery.SqlServer", sqlConnectionString),
				Setting("Gallery.SupportRequestSqlServer", sqlConnectionString),
				Setting("Gallery.SiteRoot", siteRoot),
				Setting("Gallery.SupportEmailSiteRoot", siteRoot)));

		doc.Save(Path.Combine(toolsBinDir, "appsettings.Aspire.config"));

		static XElement Setting(string key, string value) =>
			new("add", new XAttribute("key", key), new XAttribute("value", value));
	}

	static async Task<ExecuteCommandResult> DropDatabaseAsync(
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
}

/// <summary>Lightweight resource used purely for visual grouping in the Aspire dashboard.</summary>
sealed class GroupResource(string name) : Resource(name);

