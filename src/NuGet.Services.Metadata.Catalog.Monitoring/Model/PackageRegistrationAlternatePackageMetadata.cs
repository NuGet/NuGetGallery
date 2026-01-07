// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Monitoring.Model
{
    public class PackageRegistrationAlternatePackageMetadata
    {
        public string Id { get; set; }
        public string Range { get; set; }

        /// <summary>
        /// Default constructor for JSON serialization purposes.
        /// </summary>
        public PackageRegistrationAlternatePackageMetadata()
        {
        }

        /// <summary>
        /// Converts a <see cref="PackageDeprecationItem"/> into a format that can be directly compared to a <see cref="PackageRegistrationIndexMetadata"/>.
        /// </summary>
        public PackageRegistrationAlternatePackageMetadata(PackageDeprecationItem deprecation)
        {
            Id = deprecation.AlternatePackageId;
            Range = deprecation.AlternatePackageRange;
        }
    }
}
