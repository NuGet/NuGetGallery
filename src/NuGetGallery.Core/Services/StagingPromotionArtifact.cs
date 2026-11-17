// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Captures an artifact's promotion result before its staging record is removed.
    /// </summary>
    public class StagingPromotionArtifact
    {
        public StagingPromotionArtifact(Package package, bool symbols, bool succeeded)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null || string.IsNullOrWhiteSpace(package.Id) || string.IsNullOrWhiteSpace(package.NormalizedVersion))
            {
                throw new ArgumentException("The package ID and normalized version are required.", nameof(package));
            }

            PackageId = package.Id;
            Version = package.NormalizedVersion;
            Symbols = symbols;
            Succeeded = succeeded;
        }

        public string PackageId { get; }

        public string Version { get; }

        public bool Symbols { get; }

        public bool Succeeded { get; }
    }
}
