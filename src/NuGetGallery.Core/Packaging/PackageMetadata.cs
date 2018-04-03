// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGetGallery.Packaging
{
    public class PackageMetadata
    {
        /// <summary>
        /// These are properties generated in the V3 pipeline (feed2catalog job) and could collide if the .nuspec
        /// itself also contains these properties.
        /// </summary>
        private static readonly HashSet<string> RestrictedMetadataElements = new HashSet<string>
        {
            "created",
            "dependencyGroups", // Not to be confused with the valid element "dependencies" with a sub-element "group".
            "isPrerelease",
            "lastEdited",
            "listed",
            "packageEntries",
            "packageHash",
            "packageHashAlgorithm",
            "packageSize",
            "published",
            "supportedFrameworks",
            "verbatimVersion",
        };

        private static readonly HashSet<string> BooleanMetadataElements = new HashSet<string>
        {
            "developmentDependency",
            "requireLicenseAcceptance",
            "serviceable",
        };

        private readonly Dictionary<string, string> _metadata;
        private readonly IReadOnlyCollection<PackageDependencyGroup> _dependencyGroups;
        private readonly IReadOnlyCollection<FrameworkSpecificGroup> _frameworkReferenceGroups;
        private readonly IReadOnlyCollection<NuGet.Packaging.Core.PackageType> _packageTypes;

        public PackageMetadata(
            Dictionary<string, string> metadata,
            IEnumerable<PackageDependencyGroup> dependencyGroups,
            IEnumerable<FrameworkSpecificGroup> frameworkGroups,
            IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes,
            NuGetVersion minClientVersion)
        {
            _metadata = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
            _dependencyGroups = dependencyGroups.ToList().AsReadOnly();
            _frameworkReferenceGroups = frameworkGroups.ToList().AsReadOnly();
            _packageTypes = packageTypes.ToList().AsReadOnly();

            SetPropertiesFromMetadata();
            MinClientVersion = minClientVersion;
        }

        private void SetPropertiesFromMetadata()
        {
            Id = GetValue("id", string.Empty);

            var versionString = GetValue("version", string.Empty);
            if (versionString.IndexOf('.') < 0)
            {
                throw new FormatException(string.Format(CoreStrings.PackageMetadata_VersionStringInvalid, versionString));
            }

            NuGetVersion nugetVersion;
            if (NuGetVersion.TryParse(versionString, out nugetVersion))
            {
                Version = nugetVersion;
            }

            IconUrl = GetValue(PackageMetadataStrings.IconUrl, (Uri)null);
            ProjectUrl = GetValue(PackageMetadataStrings.ProjectUrl, (Uri)null);
            LicenseUrl = GetValue(PackageMetadataStrings.LicenseUrl, (Uri)null);
            Copyright = GetValue(PackageMetadataStrings.Copyright, (string)null);
            Description = GetValue(PackageMetadataStrings.Description, (string)null);
            ReleaseNotes = GetValue(PackageMetadataStrings.ReleaseNotes, (string)null);
            RequireLicenseAcceptance = GetValue(PackageMetadataStrings.RequireLicenseAcceptance, false);
            Summary = GetValue(PackageMetadataStrings.Summary, (string)null);
            Title = GetValue(PackageMetadataStrings.Title, (string)null);
            Tags = GetValue(PackageMetadataStrings.Tags, (string)null);
            Language = GetValue(PackageMetadataStrings.Language, (string)null);

            Owners = GetValue(PackageMetadataStrings.Owners, (string)null);

            var authorsString = GetValue(PackageMetadataStrings.Authors, Owners ?? string.Empty);
            Authors = new List<string>(authorsString.Split(',').Select(author => author.Trim()));
        }

        public string Id { get; private set; }
        public NuGetVersion Version { get; private set; }

        public Uri IconUrl { get; private set; }
        public Uri ProjectUrl { get; private set; }
        public Uri LicenseUrl { get; private set; }
        public string Copyright { get; private set; }
        public string Description { get; private set; }
        public string ReleaseNotes { get; private set; }
        public bool RequireLicenseAcceptance { get; private set; }
        public string Summary { get; private set; }
        public string Title { get; private set; }
        public string Tags { get; private set; }
        public List<string> Authors { get; private set; }
        public string Owners { get; private set; }
        public string Language { get; private set; }
        public NuGetVersion MinClientVersion { get; set; }

        public string GetValueFromMetadata(string key)
        {
            return GetValue(key, (string)null);
        }

        public IReadOnlyCollection<PackageDependencyGroup> GetDependencyGroups()
        {
            return _dependencyGroups;
        }

        public IReadOnlyCollection<FrameworkSpecificGroup> GetFrameworkReferenceGroups()
        {
            return _frameworkReferenceGroups;
        }

        public IReadOnlyCollection<NuGet.Packaging.Core.PackageType> GetPackageTypes()
        {
            return _packageTypes;
        }

        private string GetValue(string key, string alternateValue)
        {
            string value;
            if (_metadata.TryGetValue(key, out value))
            {
                return value;
            }

            return alternateValue;
        }

        private bool GetValue(string key, bool alternateValue)
        {
            var value = GetValue(key, alternateValue.ToString());

            bool result;
            if (bool.TryParse(value, out result))
            {
                return result;
            }

            return alternateValue;
        }

        private Uri GetValue(string key, Uri alternateValue)
        {
            var value = GetValue(key, (string)null);
            if (!string.IsNullOrEmpty(value))
            {
                Uri result;
                if (Uri.TryCreate(value, UriKind.Absolute, out result))
                {
                    return result;
                }
            }

            return alternateValue;
        }

        /// <summary>
        /// Gets package metadata from a the provided <see cref="NuspecReader"/> instance.
        /// </summary>
        /// <param name="nuspecReader">The <see cref="NuspecReader"/> instance used to read the <see cref="PackageMetadata"/></param>
        /// <param name="strict">
        /// Whether or not to be strict when reading the <see cref="NuspecReader"/>. This should be <code>true</code>
        /// on initial ingestion but false when a package that has already been accepted is being processed.</param>
        /// <exception cref="PackagingException">
        /// We default to use a strict version-check on dependency groups. 
        /// When an invalid dependency version range is detected, a <see cref="PackagingException"/> will be thrown.
        /// </exception>
        public static PackageMetadata FromNuspecReader(NuspecReader nuspecReader, bool strict)
        {
            if (strict)
            {
                var strictNuspecReader = new StrictNuspecReader(nuspecReader.Xml);

                var duplicates = strictNuspecReader.GetDuplicateMetadataElementNames();
                if (duplicates.Any())
                {
                    throw new PackagingException(string.Format(
                        CoreStrings.Manifest_DuplicateMetadataElements,
                        string.Join("', '", duplicates)));
                }
            }

            var metadata = nuspecReader
                .GetMetadata()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Reject invalid metadata element names. Today this only rejects element names that collide with
            // properties generated downstream.
            var metadataKeys = new HashSet<string>(metadata.Keys);
            metadataKeys.IntersectWith(RestrictedMetadataElements);
            if (metadataKeys.Any())
            {
                throw new PackagingException(string.Format(
                    CoreStrings.Manifest_InvalidMetadataElements,
                    string.Join("', '", metadataKeys.OrderBy(x => x))));
            }

            // Reject non-boolean values for boolean metadata.
            foreach (var booleanName in BooleanMetadataElements)
            {
                if (metadata.TryGetValue(booleanName, out var unparsedBoolean)
                    && !bool.TryParse(unparsedBoolean, out var parsedBoolean))
                {
                    throw new PackagingException(string.Format(
                        CoreStrings.Manifest_InvalidBooleanMetadata,
                        booleanName));
                }
            }

            return new PackageMetadata(
                metadata,
                nuspecReader.GetDependencyGroups(useStrictVersionCheck: strict),
                nuspecReader.GetFrameworkReferenceGroups(),
                nuspecReader.GetPackageTypes(),
                nuspecReader.GetMinClientVersion()
           );
        }

        private class StrictNuspecReader : NuspecReader
        {
            public StrictNuspecReader(XDocument xml) : base(xml)
            {
            }

            public IReadOnlyList<string> GetDuplicateMetadataElementNames()
            {
                return MetadataNode
                    .Elements()
                    .GroupBy(element => element.Name.LocalName)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToList();
            }
        }
    }
}