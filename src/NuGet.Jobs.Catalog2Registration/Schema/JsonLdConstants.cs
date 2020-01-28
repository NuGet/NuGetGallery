// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;

namespace NuGet.Jobs.Catalog2Registration
{
    public static class JsonLdConstants
    {
        public static readonly List<string> RegistrationIndexTypes = new List<string>
        {
            "catalog:CatalogRoot",
            "PackageRegistration",
            "catalog:Permalink"
        };

        public static readonly string RegistrationPageType = "catalog:CatalogPage";

        public static readonly string RegistrationLeafItemType = "Package";

        public static readonly List<string> RegistrationLeafTypes = new List<string>
        {
            "Package",
            "http://schema.nuget.org/catalog#Permalink"
        };

        public static readonly string RegistrationLeafItemCatalogEntryType = "PackageDetails";

        public static readonly string PackageDeprecationType = "deprecation";

        public static readonly string AlternatePackageType = "alternatePackage";

        public static readonly RegistrationContainerContext RegistrationContainerContext = new RegistrationContainerContext
        {
            Vocab = "http://schema.nuget.org/schema#",
            Catalog = "http://schema.nuget.org/catalog#",
            Xsd = "http://www.w3.org/2001/XMLSchema#",
            Items = new ContextTypeDescription
            {
                Id = "catalog:item",
                Container = "@set",
            },
            CommitTimestamp = new ContextTypeDescription
            {
                Id = "catalog:commitTimeStamp",
                Type = "xsd:dateTime",
            },
            CommitId = new ContextTypeDescription
            {
                Id = "catalog:commitId",
            },
            Count = new ContextTypeDescription
            {
                Id = "catalog:count",
            },
            Parent = new ContextTypeDescription
            {
                Id = "catalog:parent",
                Type = "@id",
            },
            Tags = new ContextTypeDescription
            {
                Container = "@set",
                Id = "tag",
            },
            Reasons = new ContextTypeDescription
            {
                Container = "@set"
            },
            PackageTargetFrameworks = new ContextTypeDescription
            {
                Container = "@set",
                Id = "packageTargetFramework",
            },
            DependencyGroups = new ContextTypeDescription
            {
                Container = "@set",
                Id = "dependencyGroup",
            },
            Dependencies = new ContextTypeDescription
            {
                Container = "@set",
                Id = "dependency",
            },
            PackageContent = new ContextTypeDescription
            {
                Type = "@id",
            },
            Published = new ContextTypeDescription
            {
                Type = "xsd:dateTime",
            },
            Registration = new ContextTypeDescription
            {
                Type = "@id",
            },
        };

        public static readonly RegistrationLeafContext RegistrationLeafContext = new RegistrationLeafContext
        {
            Vocab = "http://schema.nuget.org/schema#",
            Xsd = "http://www.w3.org/2001/XMLSchema#",
            CatalogEntry = new ContextTypeDescription
            {
                Type = "@id",
            },
            Registration = new ContextTypeDescription
            {
                Type = "@id",
            },
            PackageContent = new ContextTypeDescription
            {
                Type = "@id",
            },
            Published = new ContextTypeDescription
            {
                Type = "xsd:dateTime",
            },
        };
    }
}
