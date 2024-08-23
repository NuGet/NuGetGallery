// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Model
{
    public class PackageRegistrationDeprecationMetadata
    {
        public IEnumerable<string> Reasons { get; set; }
        public string Message { get; set; }
        public PackageRegistrationAlternatePackageMetadata AlternatePackage { get; set; }

        /// <summary>
        /// Default constructor for JSON serialization purposes.
        /// </summary>
        public PackageRegistrationDeprecationMetadata()
        {
        }

        /// <summary>
        /// Converts a <see cref="PackageDeprecationItem"/> into a format that can be directly compared to a <see cref="PackageRegistrationIndexMetadata"/>.
        /// </summary>
        public PackageRegistrationDeprecationMetadata(PackageDeprecationItem deprecation)
        {
            Reasons = deprecation.Reasons;
            Message = deprecation.Message;
            if (deprecation.AlternatePackageId != null || deprecation.AlternatePackageRange != null)
            {
                AlternatePackage = new PackageRegistrationAlternatePackageMetadata(deprecation);
            }
        }
    }
}
