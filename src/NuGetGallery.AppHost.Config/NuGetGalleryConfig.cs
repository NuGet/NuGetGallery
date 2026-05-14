// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

/// <summary>
/// Strongly-typed representation of the "NuGetGallery" section in appsettings.json.
/// Used by the AppHost and single-file tools via environment variable binding.
/// </summary>
public class NuGetGalleryConfig
{
	public string GalleryBaseAddress { get; set; } = "";
	public string SearchServiceBaseAddress { get; set; } = "";
	public string StorageConnectionString { get; set; } = "";
	public string AzuriteBase { get; set; } = "";
	public string SearchServiceName { get; set; } = "";
	public GalleryDbConfig GalleryDb { get; set; } = new();
	public ContainerNames Containers { get; set; } = new();
	public SearchIndexNames SearchIndexes { get; set; } = new();
	public AuxiliaryBlobNames AuxiliaryBlobs { get; set; } = new();
	public ScoringConfig Scoring { get; set; } = new();
	public PipelineSettings Settings { get; set; } = new();
}

public class GalleryDbConfig
{
	public string ConnectionString { get; set; } = "";
}

public class ContainerNames
{
	public string Catalog { get; set; } = "";
	public string FlatContainer { get; set; } = "";
	public string RegistrationSemVer1 { get; set; } = "";
	public string RegistrationGzSemVer1 { get; set; } = "";
	public string RegistrationGzSemVer2 { get; set; } = "";
	public string AzureSearch { get; set; } = "";
	public string ServiceIndex { get; set; } = "";
	public string CdnStats { get; set; } = "";
	public string SearchAuxiliary { get; set; } = "";
	public string Packages { get; set; } = "";
	public string Auditing { get; set; } = "";
	public string Content { get; set; } = "";
	public string Uploads { get; set; } = "";
}

public class SearchIndexNames
{
	public string Search { get; set; } = "";
	public string Hijack { get; set; } = "";
}

public class AuxiliaryBlobNames
{
	public string DownloadsV1Json { get; set; } = "";
	public string ExcludedPackagesJson { get; set; } = "";
	public string FlagsJson { get; set; } = "";
}

/// <summary>
/// Mirrors <c>NuGet.Services.AzureSearch.AzureSearchScoringConfiguration</c> so the
/// AppHost can bind and forward it without referencing the net472 assembly directly.
/// </summary>
public class ScoringConfig
{
	public Dictionary<string, double> FieldWeights { get; set; } = new();
	public double PopularityTransfer { get; set; }
	public double DownloadScoreBoost { get; set; }
}

public class PipelineSettings
{
	public int PollIntervalSeconds { get; set; }
	public int CursorSize { get; set; }
	public string MinPushPeriod { get; set; } = "";
	public int MaxDownloadCountDecreases { get; set; }
	public bool EnablePopularityTransfers { get; set; }
}
