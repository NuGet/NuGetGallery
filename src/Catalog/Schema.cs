// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog
{
    public static class Schema
    {
        public static class Prefixes
        {
            public static readonly string NuGet = "http://schema.nuget.org/schema#";
            public static readonly string Catalog = "http://schema.nuget.org/catalog#";
            public static readonly string Xsd = "http://www.w3.org/2001/XMLSchema#";
            public static readonly string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        }

        public static class DataTypes
        {
            public static readonly Uri PackageDetails = new Uri(Prefixes.NuGet + "PackageDetails");
            public static readonly Uri PackageDelete = new Uri(Prefixes.NuGet + "PackageDelete");

            public static readonly Uri PackageEntry = new Uri(Prefixes.NuGet + "PackageEntry");

            public static readonly Uri CatalogRoot = new Uri(Prefixes.Catalog + "CatalogRoot");
            public static readonly Uri CatalogPage = new Uri(Prefixes.Catalog + "CatalogPage");
            public static readonly Uri Permalink = new Uri(Prefixes.Catalog + "Permalink");
            public static readonly Uri AppendOnlyCatalog = new Uri(Prefixes.Catalog + "AppendOnlyCatalog");

            public static readonly Uri Integer = new Uri(Prefixes.Xsd + "integer");
            public static readonly Uri DateTime = new Uri(Prefixes.Xsd + "dateTime");
            public static readonly Uri Boolean = new Uri(Prefixes.Xsd + "boolean");

            public static readonly Uri Vulnerability = new Uri(Prefixes.NuGet + "Vulnerability");
        }

        public static class Predicates
        {
            public static readonly Uri Type = new Uri(Prefixes.Rdf + "type");

            public static readonly Uri CatalogCommitId = new Uri(Prefixes.Catalog + "commitId");
            public static readonly Uri CatalogTimeStamp = new Uri(Prefixes.Catalog + "commitTimeStamp");
            public static readonly Uri CatalogItem = new Uri(Prefixes.Catalog + "item");
            public static readonly Uri CatalogCount = new Uri(Prefixes.Catalog + "count");
            public static readonly Uri CatalogParent = new Uri(Prefixes.Catalog + "parent");

            public static readonly Uri Id = new Uri(Prefixes.NuGet + "id");
            public static readonly Uri Version = new Uri(Prefixes.NuGet + "version");
            public static readonly Uri OriginalId = new Uri(Prefixes.NuGet + "originalId");

            public static readonly Uri PackageEntry = new Uri(Prefixes.NuGet + "packageEntry");
            public static readonly Uri FullName = new Uri(Prefixes.NuGet + "fullName");
            public static readonly Uri Name = new Uri(Prefixes.NuGet + "name");
            public static readonly Uri Length = new Uri(Prefixes.NuGet + "length");
            public static readonly Uri CompressedLength = new Uri(Prefixes.NuGet + "compressedLength");

            // General-purpose fields used in C# explicitly (not just in the .nuspec to RDF XSLT)
            public static readonly Uri Created = new Uri(Prefixes.NuGet + "created");

            public static readonly Uri LastCreated = new Uri(Prefixes.NuGet + "lastCreated");
            public static readonly Uri LastEdited = new Uri(Prefixes.NuGet + "lastEdited");
            public static readonly Uri LastDeleted = new Uri(Prefixes.NuGet + "lastDeleted");
            public static readonly Uri Listed = new Uri(Prefixes.NuGet + "listed");

            public static readonly Uri Published = new Uri(Prefixes.NuGet + "published");
            public static readonly Uri PackageHash = new Uri(Prefixes.NuGet + "packageHash");
            public static readonly Uri PackageHashAlgorithm = new Uri(Prefixes.NuGet + "packageHashAlgorithm");
            public static readonly Uri PackageSize = new Uri(Prefixes.NuGet + "packageSize");

            public static readonly Uri Range = new Uri(Prefixes.NuGet + "range");

            public static readonly Uri Deprecation = new Uri(Prefixes.NuGet + "deprecation");

            public static readonly Uri Reasons = new Uri(Prefixes.NuGet + "reasons");
            public static readonly Uri Message = new Uri(Prefixes.NuGet + "message");
            public static readonly Uri AlternatePackage = new Uri(Prefixes.NuGet + "alternatePackage");

            public static readonly Uri Vulnerability = new Uri(Prefixes.NuGet + "vulnerability");
            public static readonly Uri AdvisoryUrl = new Uri(Prefixes.NuGet + "advisoryUrl");
            public static readonly Uri Severity = new Uri(Prefixes.NuGet + "severity");
        }
    }
}
