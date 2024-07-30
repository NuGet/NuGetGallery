// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;
using NgTests.Infrastructure;

namespace CatalogTests.Helpers
{
    internal sealed class CatalogIndependentPackageDetails
    {
        private static readonly JObject _context = JObject.Parse(
@"{
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""catalog"": ""http://schema.nuget.org/catalog#"",
    ""xsd"": ""http://www.w3.org/2001/XMLSchema#"",
    ""dependencies"": {
      ""@id"": ""dependency"",
      ""@container"": ""@set""
    },
    ""dependencyGroups"": {
      ""@id"": ""dependencyGroup"",
      ""@container"": ""@set""
    },
    ""packageEntries"": {
      ""@id"": ""packageEntry"",
      ""@container"": ""@set""
    },
    ""packageTypes"": {
      ""@id"": ""packageType"",
      ""@container"": ""@set""
    },
    ""supportedFrameworks"": {
      ""@id"": ""supportedFramework"",
      ""@container"": ""@set""
    },
    ""tags"": {
      ""@id"": ""tag"",
      ""@container"": ""@set""
    },
    ""published"": {
      ""@type"": ""xsd:dateTime""
    },
    ""created"": {
      ""@type"": ""xsd:dateTime""
    },
    ""lastEdited"": {
      ""@type"": ""xsd:dateTime""
    },
    ""catalog:commitTimeStamp"": {
      ""@type"": ""xsd:dateTime""
    }
  }");

        [JsonProperty(CatalogConstants.IdKeyword)]
        internal string IdKeyword { get; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        internal string[] TypeKeyword { get; }
        [JsonProperty(CatalogConstants.Authors)]
        internal string Authors { get; }
        [JsonProperty(CatalogConstants.CatalogCommitId)]
        internal string CommitId { get; }
        [JsonProperty(CatalogConstants.CatalogCommitTimeStamp)]
        internal string CommitTimeStamp { get; }
        [JsonProperty(CatalogConstants.Created)]
        internal string Created { get; }
        [JsonProperty(CatalogConstants.Deprecation)]
        internal RegistrationPackageDeprecation Deprecation { get; }
        [JsonProperty(CatalogConstants.Description)]
        internal string Description { get; }
        [JsonProperty(CatalogConstants.Id)]
        internal string Id { get; }
        [JsonProperty(CatalogConstants.IsPrerelease)]
        internal bool IsPrerelease { get; }
        [JsonProperty(CatalogConstants.LastEdited)]
        internal string LastEdited { get; }
        [JsonProperty(CatalogConstants.Listed)]
        internal bool Listed { get; }
        [JsonProperty(CatalogConstants.PackageHash)]
        internal string PackageHash { get; }
        [JsonProperty(CatalogConstants.PackageHashAlgorithm)]
        internal string PackageHashAlgorithm { get; }
        [JsonProperty(CatalogConstants.PackageSize)]
        internal int PackageSize { get; }
        [JsonProperty(CatalogConstants.Published)]
        internal string Published { get; }
        [JsonProperty(CatalogConstants.RequireLicenseAcceptance)]
        internal bool RequireLicenseAcceptance { get; }
        [JsonProperty(CatalogConstants.VerbatimVersion)]
        internal string VerbatimVersion { get; }
        [JsonProperty(CatalogConstants.Version)]
        internal string Version { get; }
        [JsonProperty(CatalogConstants.PackageEntries)]
        internal CatalogPackageEntry[] PackageEntries { get; }
        [JsonProperty(CatalogConstants.ContextKeyword)]
        internal JObject ContextKeyword { get; }

        internal CatalogIndependentPackageDetails(
            string id = null,
            string version = null,
            string baseUri = null,
            string commitId = null,
            DateTimeOffset? commitTimeStamp = null,
            RegistrationPackageDeprecation deprecation = null)
        {
            var utc = commitTimeStamp ?? DateTimeOffset.UtcNow;

            Id = id ?? TestUtility.CreateRandomAlphanumericString();

            var build = (int)(utc.Ticks & 0xff); // random number

            VerbatimVersion = version ?? $"1.0.{build}";
            Version = version ?? VerbatimVersion;

            IdKeyword = $"{baseUri ?? "https://nuget.test/"}" +
                $"v3-catalog0/data/{utc.ToString(CatalogConstants.UrlTimeStampFormat)}" +
                $"/{Id.ToLowerInvariant()}.{Version.ToLowerInvariant()}.json";
            TypeKeyword = new[] { CatalogConstants.PackageDetails, CatalogConstants.CatalogPermalink };
            Authors = TestUtility.CreateRandomAlphanumericString();
            CommitId = commitId ?? Guid.NewGuid().ToString("D");
            CommitTimeStamp = utc.ToString(CatalogConstants.CommitTimeStampFormat);
            Created = utc.AddHours(-2).ToString(CatalogConstants.DateTimeFormat);
            Deprecation = deprecation;
            Description = TestUtility.CreateRandomAlphanumericString();
            LastEdited = utc.AddHours(-1).ToString(CatalogConstants.DateTimeFormat);
            Listed = true;
            PackageHash = CreateFakePackageHash();
            PackageHashAlgorithm = TestUtility.CreateRandomAlphanumericString();
            PackageSize = (int)(utc.Ticks & 0xffffff);  // random number
            Published = Created;
            RequireLicenseAcceptance = utc.Ticks % 2 == 0;

            PackageEntries = new[]
            {
                new CatalogPackageEntry(
                    idKeyword: $"{IdKeyword}#{Id}.nuspec",
                    typeKeyword: CatalogConstants.PackageEntry,
                    compressedLength: (int)(utc.Ticks & 0xffff) + 1,
                    fullName: $"{Id}.nuspec",
                    length: (int)(utc.Ticks & 0xfff) + 1,
                    name: $"{Id}.nuspec"),
                new CatalogPackageEntry(
                    idKeyword: $"{IdKeyword}#.signature.p7s",
                    typeKeyword: CatalogConstants.PackageEntry,
                    compressedLength: (int)(utc.Ticks & 0xffff),
                    fullName: ".signature.p7s",
                    length: (int)(utc.Ticks & 0xfff),
                    name: ".signature.p7s")
            };

            ContextKeyword = _context;
        }

        [JsonConstructor]
        internal CatalogIndependentPackageDetails(
            string idKeyword,
            string[] typeKeyword,
            string authors,
            string commitId,
            string commitTimeStamp,
            string created,
            string description,
            string id,
            bool isPrerelease,
            string lastEdited,
            bool listed,
            string packageHash,
            string packageHashAlgorithm,
            int packageSize,
            string published,
            bool requireLicenseAcceptance,
            string verbatimVersion,
            string version,
            CatalogPackageEntry[] packageEntries,
            JObject contextKeyword)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            Authors = authors;
            CommitId = commitId;
            CommitTimeStamp = commitTimeStamp;
            Created = created;
            Description = description;
            Id = id;
            IsPrerelease = isPrerelease;
            LastEdited = lastEdited;
            Listed = listed;
            PackageHash = packageHash;
            PackageHashAlgorithm = packageHashAlgorithm;
            PackageSize = packageSize;
            Published = published;
            RequireLicenseAcceptance = requireLicenseAcceptance;
            VerbatimVersion = verbatimVersion;
            Version = version;
            PackageEntries = packageEntries;
            ContextKeyword = contextKeyword;
        }

        private static string CreateFakePackageHash()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[64];

                rng.GetBytes(bytes);

                return Convert.ToBase64String(bytes);
            }
        }
    }
}