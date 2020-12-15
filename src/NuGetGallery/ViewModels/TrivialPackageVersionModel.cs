// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class TrivialPackageVersionModel : IPackageVersionModel
    {
        public TrivialPackageVersionModel(Package package)
        {
            Id = package.PackageRegistration.Id;
            Version = package.NormalizedVersion;
            HasEmbeddedReadme = package.HasEmbeddedReadme;
        }

        public TrivialPackageVersionModel(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; set; }
        public string Version { get; set; }
        public bool HasEmbeddedReadme { get; set; }
    }
}