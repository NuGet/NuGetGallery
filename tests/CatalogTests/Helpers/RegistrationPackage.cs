// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class RegistrationPackage
    {
        [JsonProperty(CatalogConstants.IdKeyword)]
        internal string IdKeyword { get; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        internal string TypeKeyword { get; }
        [JsonProperty(CatalogConstants.CommitId)]
        internal string CommitId { get; }
        [JsonProperty(CatalogConstants.CommitTimeStamp)]
        internal string CommitTimeStamp { get; }
        [JsonProperty(CatalogConstants.CatalogEntry)]
        internal RegistrationPackageDetails CatalogEntry { get; }
        [JsonProperty(CatalogConstants.PackageContent)]
        internal string PackageContent { get; }
        [JsonProperty(CatalogConstants.Registration)]
        internal string Registration { get; }

        [JsonConstructor]
        internal RegistrationPackage(
            string idKeyword,
            string typeKeyword,
            string commitId,
            string commitTimeStamp,
            RegistrationPackageDetails catalogEntry,
            string packageContent,
            string registration)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            CommitId = commitId;
            CommitTimeStamp = commitTimeStamp;
            CatalogEntry = catalogEntry;
            PackageContent = packageContent;
            Registration = registration;
        }
    }
}