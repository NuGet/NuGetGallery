// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class RegistrationPackageDetails
    {
        [JsonProperty(CatalogConstants.IdKeyword)]
        internal string IdKeyword { get; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        internal string TypeKeyword { get; }
        [JsonProperty(CatalogConstants.Authors)]
        internal string Authors { get; }
        [JsonProperty(CatalogConstants.Deprecation)]
        internal RegistrationPackageDeprecation Deprecation { get; }
        [JsonProperty(CatalogConstants.Description)]
        internal string Description { get; }
        [JsonProperty(CatalogConstants.IconUrl)]
        internal string IconUrl { get; }
        [JsonProperty(CatalogConstants.Id)]
        internal string Id { get; }
        [JsonProperty(CatalogConstants.Language)]
        internal string Language { get; }
        [JsonProperty(CatalogConstants.LicenseUrl)]
        internal string LicenseUrl { get; }
        [JsonProperty(CatalogConstants.Listed)]
        internal bool Listed { get; }
        [JsonProperty(CatalogConstants.MinClientVersion)]
        internal string MinClientVersion { get; }
        [JsonProperty(CatalogConstants.PackageContent)]
        internal string PackageContent { get; }
        [JsonProperty(CatalogConstants.ProjectUrl)]
        internal string ProjectUrl { get; }
        [JsonProperty(CatalogConstants.Published)]
        internal string Published { get; }
        [JsonProperty(CatalogConstants.RequireLicenseAcceptance)]
        internal bool RequireLicenseAcceptance { get; }
        [JsonProperty(CatalogConstants.Summary)]
        internal string Summary { get; }
        [JsonProperty(CatalogConstants.Tags)]
        internal string[] Tags { get; }
        [JsonProperty(CatalogConstants.Title)]
        internal string Title { get; }
        [JsonProperty(CatalogConstants.Version)]
        internal string Version { get; }

        [JsonConstructor]
        internal RegistrationPackageDetails(
            string idKeyword,
            string typeKeyword,
            string authors,
            RegistrationPackageDeprecation deprecation,
            string description,
            string iconUrl,
            string id,
            string language,
            string licenseUrl,
            bool listed,
            string minClientVersion,
            string packageContent,
            string projectUrl,
            string published,
            bool requireLicenseAcceptance,
            string summary,
            string[] tags,
            string title,
            string version)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            Authors = authors;
            Deprecation = deprecation;
            Description = description;
            IconUrl = iconUrl;
            Id = id;
            Language = language;
            LicenseUrl = licenseUrl;
            Listed = listed;
            MinClientVersion = minClientVersion;
            PackageContent = packageContent;
            ProjectUrl = projectUrl;
            Published = published;
            RequireLicenseAcceptance = requireLicenseAcceptance;
            Summary = summary;
            Tags = tags;
            Title = title;
            Version = version;
        }
    }
}