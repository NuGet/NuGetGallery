﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Search.Documents;
using Microsoft.Extensions.Options;
using NuGet.Protocol.Registration;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchResponseBuilder : ISearchResponseBuilder
    {
        private static readonly V2SearchDependency[] EmptyDependencies = new V2SearchDependency[0];
        private readonly Lazy<IAuxiliaryData> _lazyAuxiliaryData;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IOptionsSnapshot<SearchServiceConfiguration> _options;
        private readonly FlatContainerPackagePathProvider _pathProvider;
        private readonly Uri _flatContainerBaseUrl;

        public SearchResponseBuilder(
            Lazy<IAuxiliaryData> auxiliaryData,
            IFeatureFlagService featureFlagService,
            IOptionsSnapshot<SearchServiceConfiguration> options)
        {
            _lazyAuxiliaryData = auxiliaryData ?? throw new ArgumentNullException(nameof(auxiliaryData));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (_options.Value.SemVer1RegistrationsBaseUrl == null)
            {
                throw new ArgumentException($"The {nameof(SearchServiceConfiguration.SemVer1RegistrationsBaseUrl)} needs to be set.", nameof(options));
            }

            if (_options.Value.SemVer2RegistrationsBaseUrl == null)
            {
                throw new ArgumentException($"The {nameof(SearchServiceConfiguration.SemVer2RegistrationsBaseUrl)} needs to be set.", nameof(options));
            }

            if (_options.Value.AllIconsInFlatContainer)
            {
                if (_options.Value.FlatContainerBaseUrl == null)
                {
                    throw new ArgumentException($"The {nameof(SearchServiceConfiguration.FlatContainerBaseUrl)} needs to be set.", nameof(options));
                }

                if (_options.Value.FlatContainerContainerName == null)
                {
                    throw new ArgumentException($"The {nameof(SearchServiceConfiguration.FlatContainerContainerName)} needs to be set.", nameof(options));
                }

                _pathProvider = new FlatContainerPackagePathProvider(_options.Value.FlatContainerContainerName);
                _flatContainerBaseUrl = _options.Value.ParseFlatContainerBaseUrl();
            }
        }

        private IAuxiliaryData AuxiliaryData => _lazyAuxiliaryData.Value;

        public V2SearchResponse V2FromHijack(
            V2SearchRequest request,
            string text,
            SearchOptions searchParameters,
            SingleSearchResultPage<HijackDocument.Full> result,
            TimeSpan duration)
        {
            return ToResponse(
                request,
                searchParameters,
                text,
                _options.Value.HijackIndexName,
                result,
                duration,
                p => ToV2SearchPackage(p, request.IncludeSemVer2));
        }

        public V2SearchResponse V2FromSearch(
            V2SearchRequest request,
            string text,
            SearchOptions parameters,
            SingleSearchResultPage<SearchDocument.Full> result,
            TimeSpan duration)
        {
            return ToResponse(
                request,
                parameters,
                text,
                _options.Value.SearchIndexName,
                result,
                duration,
                p => ToV2SearchPackage(p));
        }

        public V2SearchResponse V2FromSearchDocument(
            V2SearchRequest request,
            string documentKey,
            SearchDocument.Full document,
            TimeSpan duration)
        {
            return ToResponse(
                request,
                _options.Value.SearchIndexName,
                documentKey,
                document,
                duration,
                p => ToV2SearchPackage(p));
        }

        public V2SearchResponse V2FromHijackDocument(
            V2SearchRequest request,
            string documentKey,
            HijackDocument.Full document,
            TimeSpan duration)
        {
            return ToResponse(
                request,
                _options.Value.HijackIndexName,
                documentKey,
                document,
                duration,
                p => ToV2SearchPackage(p, request.IncludeSemVer2));
        }

        public V3SearchResponse V3FromSearchDocument(
            V3SearchRequest request,
            string documentKey,
            SearchDocument.Full document,
            TimeSpan duration)
        {
            var registrationsBaseUrl = GetRegistrationsBaseUrl(request.IncludeSemVer2);

            var data = new List<V3SearchPackage>();
            if (document != null)
            {
                var package = ToV3SearchPackage(document, registrationsBaseUrl);
                package.Debug = request.ShowDebug ? new DebugDocumentResult { Document = document } : null;
                data.Add(package);
            }

            return new V3SearchResponse
            {
                Context = new V3SearchContext
                {
                    Vocab = "http://schema.nuget.org/schema#",
                    Base = registrationsBaseUrl,
                },
                TotalHits = data.Count,
                Data = data,
                Debug = DebugInformation.CreateFromGetOrNull(
                    request,
                    _options.Value.SearchIndexName,
                    documentKey,
                    duration,
                    AuxiliaryData.Metadata),
            };
        }

        public V3SearchResponse V3FromSearch(
            V3SearchRequest request,
            string text,
            SearchOptions parameters,
            SingleSearchResultPage<SearchDocument.Full> result,
            TimeSpan duration)
        {
            var results = result.Values;
            result.Values = null;

            var registrationsBaseUrl = GetRegistrationsBaseUrl(request.IncludeSemVer2);

            return new V3SearchResponse
            {
                Context = GetV3SearchContext(registrationsBaseUrl),
                TotalHits = result.Count.Value,
                Data = results
                    .Select(x =>
                    {
                        var package = ToV3SearchPackage(x.Document, registrationsBaseUrl);
                        package.Debug = request.ShowDebug ? x : null;
                        return package;
                    })
                    .ToList(),
                Debug = DebugInformation.CreateFromSearchOrNull(
                    request,
                    _options.Value.SearchIndexName,
                    parameters,
                    text,
                    result,
                    duration,
                    AuxiliaryData.Metadata),
            };
        }

        public AutocompleteResponse AutocompleteFromSearch(
            AutocompleteRequest request,
            string text,
            SearchOptions parameters,
            SingleSearchResultPage<SearchDocument.Full> result,
            TimeSpan duration)
        {
            var results = result.Values;
            result.Values = null;

            List<string> data;
            switch (request.Type)
            {
                case AutocompleteRequestType.PackageIds:
                    data = results.Select(x => x.Document.PackageId).ToList();
                    break;

                case AutocompleteRequestType.PackageVersions:
                    if (result.Count > 1 || results.Count > 1)
                    {
                        throw new ArgumentException(
                            "Package version autocomplete queries should have a single document result",
                            nameof(result));
                    }

                    data = results.SelectMany(x => x.Document.Versions).ToList();
                    break;

                default:
                    throw new InvalidOperationException($"Unknown autocomplete request type '{request.Type}'");
            }

            return new AutocompleteResponse
            {
                Context = GetAutocompleteContext(),
                TotalHits = result.Count.Value,
                Data = data,
                Debug = DebugInformation.CreateFromSearchOrNull(
                    request,
                    _options.Value.SearchIndexName,
                    parameters,
                    text,
                    result,
                    duration,
                    auxiliaryFilesMetadata: null),
            };
        }

        private static string TitleThenId(IBaseMetadataDocument document)
        {
            if (!string.IsNullOrWhiteSpace(document.Title))
            {
                return document.Title;
            }

            return document.PackageId;
        }

        private V2SearchResponse ToResponse<T>(
            V2SearchRequest request,
            string indexName,
            string documentKey,
            T document,
            TimeSpan duration,
            Func<T, V2SearchPackage> toPackage)
            where T : class
        {
            var data = new List<V2SearchPackage>();
            if (document != null)
            {
                var package = toPackage(document);
                package.Debug = request.ShowDebug ? new DebugDocumentResult { Document = document } : null;
                data.Add(package);
            }

            if (request.CountOnly)
            {
                return new V2SearchResponse
                {
                    TotalHits = data.Count,
                    Debug = DebugInformation.CreateFromGetOrNull(
                        request,
                        indexName,
                        documentKey,
                        duration,
                        AuxiliaryData.Metadata),
                };
            }
            else
            {
                return new V2SearchResponse
                {
                    TotalHits = data.Count,
                    Data = data,
                    Debug = DebugInformation.CreateFromGetOrNull(
                        request,
                        indexName,
                        documentKey,
                        duration,
                        AuxiliaryData.Metadata),
                };
            }
        }

        private V2SearchResponse ToResponse<T>(
            V2SearchRequest request,
            SearchOptions parameters,
            string text,
            string indexName,
            SingleSearchResultPage<T> result,
            TimeSpan duration,
            Func<T, V2SearchPackage> toPackage)
            where T : class
        {
            var results = result.Values;
            result.Values = null;

            if (request.CountOnly)
            {
                return new V2SearchResponse
                {
                    TotalHits = result.Count.Value,
                    Debug = DebugInformation.CreateFromSearchOrNull(
                        request,
                        indexName,
                        parameters,
                        text,
                        result,
                        duration,
                        AuxiliaryData.Metadata),
                };
            }
            else
            {
                var resultData = results.Select(x =>
                {
                    var package = toPackage(x.Document);
                    package.Debug = request.ShowDebug ? x : null;
                    return package;
                });

                // The real sorting happens in Azure Search. However, we do another round of sorting here since the sorting 
                // on Azure Search index's downloads may produce different results from what customers see on 
                // the Gallery (which uses Auxiliary file's download count)
                if (request.SortBy == V2SortBy.TotalDownloadsAsc)
                {
                    resultData = resultData.OrderBy(x => x.PackageRegistration.DownloadCount).ThenBy(x => x.Created);   
                }
                else if (request.SortBy == V2SortBy.TotalDownloadsDesc)
                {
                    resultData = resultData.OrderByDescending(x => x.PackageRegistration.DownloadCount).ThenByDescending(x => x.Created);
                }

                return new V2SearchResponse
                {
                    TotalHits = result.Count.Value,
                    Data = resultData
                        .ToList(),
                    Debug = DebugInformation.CreateFromSearchOrNull(
                        request,
                        indexName,
                        parameters,
                        text,
                        result,
                        duration,
                        AuxiliaryData.Metadata),
                };
            }
        }

        private V3SearchPackage ToV3SearchPackage(SearchDocument.Full result, string registrationsBaseUrl)
        {
            var registrationIndexUrl = RegistrationUrlBuilder.GetIndexUrl(registrationsBaseUrl, result.PackageId);
            var package = new V3SearchPackage
            {
                AtId = registrationIndexUrl,
                Type = "Package",
                Registration = registrationIndexUrl,
                Id = result.PackageId,
                Version = result.FullVersion,
                Description = result.Description ?? string.Empty,
                Summary = result.Summary ?? string.Empty,
                Title = TitleThenId(result),
                IconUrl = GetIconUrl(result),
                LicenseUrl = result.LicenseUrl,
                ProjectUrl = result.ProjectUrl,
                Tags = result.Tags ?? Array.Empty<string>(),
                Authors = new[] { result.Authors ?? string.Empty },
                TotalDownloads = AuxiliaryData.GetTotalDownloadCount(result.PackageId),
                Verified = AuxiliaryData.IsVerified(result.PackageId),
                PackageTypes = GetV3SearchPackageTypes(result),
                Versions = result
                    .Versions
                    .Select(x =>
                    {
                        // Each of these versions is the full version.
                        var lowerVersion = NuGetVersion.Parse(x).ToNormalizedString().ToLowerInvariant();
                        return new V3SearchVersion
                        {
                            Version = x,
                            Downloads = AuxiliaryData.GetDownloadCount(result.PackageId, lowerVersion),
                            AtId = RegistrationUrlBuilder.GetLeafUrl(registrationsBaseUrl, result.PackageId, x),
                        };
                    })
                    .ToList(),
                Deprecation = GetV3SearchDeprecation(result),
                Vulnerabilities = GetV3SearchVulnerabilities(result),
            };

            if (_featureFlagService.IsV3OwnersPropertyEnabled())
            {
                package.Owners = result.Owners ?? Array.Empty<string>();
            }

            return package;
        }

        private List<V3SearchPackageType> GetV3SearchPackageTypes(SearchDocument.UpdateLatest document)
        {
            if (document.PackageTypes == null || document.PackageTypes.Length == 0)
            {
                return null;
            }

            var packageTypes = new List<V3SearchPackageType>(document.PackageTypes.Length);
            foreach (var packageType in document.PackageTypes)
            {
                packageTypes.Add(new V3SearchPackageType { Name = packageType });
            }

            return packageTypes;
        }

        private string GetIconUrl(IBaseMetadataDocument document)
        {
            // If we mandate that all icon URLs come from flat container, generate the URL.
            if (_options.Value.AllIconsInFlatContainer
                && !string.IsNullOrWhiteSpace(document.IconUrl))
            {
                var iconPath = _pathProvider.GetIconPath(document.PackageId, document.NormalizedVersion, normalize: false);
                return new Uri(_flatContainerBaseUrl, iconPath).AbsoluteUri;
            }

            return document.IconUrl;
        }

        private V3SearchDeprecation GetV3SearchDeprecation(SearchDocument.Full document)
        {
            if (HasValidDeprecation(document.Deprecation))
            {   
                var deprecation = new V3SearchDeprecation();
                
                if (document.Deprecation.AlternatePackage != null)
                {
                    deprecation.AlternatePackage = new V3SearchAlternatePackage 
                    {
                        Id = document.Deprecation.AlternatePackage.Id,
                        Range = document.Deprecation.AlternatePackage.Range
                    };
                } 

                deprecation.Message = document.Deprecation.Message;
                deprecation.Reasons = document.Deprecation.Reasons;

                return deprecation;
            }

            return null;
        }

        private List<V3SearchVulnerability> GetV3SearchVulnerabilities(SearchDocument.Full document)
        {
            var vulnerabilities = new List<V3SearchVulnerability>();

            if (document.Vulnerabilities != null)
            {
                foreach (var vulnerability in document.Vulnerabilities)
                {
                    vulnerabilities.Add(new V3SearchVulnerability 
                    {
                        AdvisoryUrl = vulnerability.AdvisoryURL,
                        Severity = vulnerability.Severity
                    });
                }
            }

            return vulnerabilities;
        }

        private V2SearchPackage ToV2SearchPackage(SearchDocument.Full result)
        {
            var package = BaseMetadataDocumentToPackage(result);

            package.Frameworks = result.Frameworks;
            package.Tfms = result.Tfms;
            package.ComputedFrameworks = result.ComputedFrameworks;
            package.ComputedTfms = result.ComputedTfms;
            package.PackageRegistration.Owners = result.Owners ?? Array.Empty<string>();
            package.Listed = true;
            package.IsLatestStable = result.IsLatestStable.Value;
            package.IsLatest = result.IsLatest.Value;
            package.Deprecation = GetV2SearchDeprecation(result);
            package.Vulnerabilities = GetV2SearchVulnerabilities(result);

            return package;
        }

        private bool HasValidDeprecation(Deprecation deprecation)
        {
            //according to nuget api, reasons array is required and cannot be empty
            return deprecation != null && deprecation.Reasons != null && deprecation.Reasons.Length > 0;
        }

        private V2SearchDeprecation GetV2SearchDeprecation(SearchDocument.Full document)
        {
            if (HasValidDeprecation(document.Deprecation))
            {
                var deprecation = new V2SearchDeprecation();
                deprecation.AlternatePackage = new V2SearchAlternatePackage();

                if (document.Deprecation.AlternatePackage != null)
                {
                    deprecation.AlternatePackage.Id = document.Deprecation.AlternatePackage.Id;
                    deprecation.AlternatePackage.Range = document.Deprecation.AlternatePackage.Range;
                }

                deprecation.Message = document.Deprecation.Message;
                deprecation.Reasons = document.Deprecation.Reasons;

                return deprecation;
            }

            return null;
        }

        private V2SearchVulnerability[] GetV2SearchVulnerabilities(SearchDocument.Full document)
        {
            var vulnerabilities = new List<V2SearchVulnerability>();

            if (document.Vulnerabilities != null)
            {
                foreach (var vulnerability in document.Vulnerabilities)
                {
                    vulnerabilities.Add(new V2SearchVulnerability
                    {
                        AdvisoryUrl = vulnerability.AdvisoryURL,
                        Severity = vulnerability.Severity
                    });
                }
            }

            return vulnerabilities.ToArray();
        }

        private V2SearchPackage ToV2SearchPackage(HijackDocument.Full result, bool semVer2)
        {
            var package = BaseMetadataDocumentToPackage(result);

            // The owners are not used in the hijack scenarios.
            package.PackageRegistration.Owners = Array.Empty<string>();

            package.Listed = result.Listed.Value;
            package.IsLatestStable = semVer2 ? result.IsLatestStableSemVer2.Value : result.IsLatestStableSemVer1.Value;
            package.IsLatest = semVer2 ? result.IsLatestSemVer2.Value : result.IsLatestSemVer1.Value;

            return package;
        }

        private V2SearchPackage BaseMetadataDocumentToPackage(IBaseMetadataDocument document)
        {
            return new V2SearchPackage
            {
                PackageRegistration = new V2SearchPackageRegistration
                {
                    Id = document.PackageId,
                    DownloadCount = AuxiliaryData.GetTotalDownloadCount(document.PackageId),
                    Verified = AuxiliaryData.IsVerified(document.PackageId),
                    PopularityTransfers = AuxiliaryData.GetPopularityTransfers(document.PackageId),
                },
                Version = document.OriginalVersion ?? document.NormalizedVersion,
                NormalizedVersion = document.NormalizedVersion,
                Title = TitleThenId(document),
                Description = document.Description ?? string.Empty,
                Summary = document.Summary ?? string.Empty,
                Authors = document.Authors ?? string.Empty,
                Copyright = document.Copyright,
                Language = document.Language,
                Tags = document.Tags != null ? string.Join(" ", document.Tags) : string.Empty,
                ReleaseNotes = document.ReleaseNotes,
                ProjectUrl = document.ProjectUrl,
                IconUrl = GetIconUrl(document),
                Created = document.Created.Value,
                Published = document.Published.Value,
                LastUpdated = document.Published.Value,
                LastEdited = document.LastEdited,
                DownloadCount = AuxiliaryData.GetDownloadCount(document.PackageId, document.NormalizedVersion),
                FlattenedDependencies = document.FlattenedDependencies,
                Dependencies = EmptyDependencies,
                SupportedFrameworks = Array.Empty<string>(),
                MinClientVersion = document.MinClientVersion,
                Hash = document.Hash,
                HashAlgorithm = document.HashAlgorithm,
                PackageFileSize = document.FileSize.Value,
                LicenseUrl = document.LicenseUrl,
                RequiresLicenseAcceptance = document.RequiresLicenseAcceptance ?? false,
            };
        }

        public string GetRegistrationsBaseUrl(bool includeSemVer2)
        {
            var url = includeSemVer2 ? _options.Value.SemVer2RegistrationsBaseUrl : _options.Value.SemVer1RegistrationsBaseUrl;

            return url.TrimEnd('/') + '/';
        }

        public V2SearchResponse EmptyV2(V2SearchRequest request)
        {
            return new V2SearchResponse
            {
                TotalHits = 0,
                Data = new List<V2SearchPackage>(),
                Debug = DebugInformation.CreateFromEmptyOrNull(request),
            };
        }

        public V3SearchResponse EmptyV3(V3SearchRequest request)
        {
            var registrationsBaseUrl = GetRegistrationsBaseUrl(request.IncludeSemVer2);

            return new V3SearchResponse
            {
                Context = GetV3SearchContext(registrationsBaseUrl),
                TotalHits = 0,
                Data = new List<V3SearchPackage>(),
                Debug = DebugInformation.CreateFromEmptyOrNull(request),
            };
        }

        public AutocompleteResponse EmptyAutocomplete(AutocompleteRequest request)
        {
            return new AutocompleteResponse
            {
                Context = GetAutocompleteContext(),
                TotalHits = 0,
                Data = new List<string>(),
                Debug = DebugInformation.CreateFromEmptyOrNull(request),
            };
        }

        private static V3SearchContext GetV3SearchContext(string registrationsBaseUrl)
        {
            return new V3SearchContext
            {
                Vocab = "http://schema.nuget.org/schema#",
                Base = registrationsBaseUrl,
            };
        }

        private static AutocompleteContext GetAutocompleteContext()
        {
            return new AutocompleteContext
            {
                Vocab = "http://schema.nuget.org/schema#",
            };
        }
    }
}
