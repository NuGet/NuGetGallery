// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Validation;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class ValidatedPackageViewModel : IPackageVersionModel
    {
        public ValidatedPackageViewModel(IReadOnlyList<PackageValidationSet> validationSets, PackageDeletedStatus deletedStatus, ValidatingType validatingType)
        {
            var first = validationSets.First();
            PackageKey = first.PackageKey;
            Id = first.PackageId;
            NormalizedVersion = first.PackageNormalizedVersion;
            DeletedStatus = deletedStatus;
            ValidationSets = validationSets;
            ValidatingType = validatingType;
        }

        public int PackageKey { get; }
        public string Id { get; }
        public string Version => NormalizedVersion;
        public string NormalizedVersion { get; }
        public PackageDeletedStatus DeletedStatus { get; }
        public IReadOnlyList<PackageValidationSet> ValidationSets { get; }

        public ValidatingType ValidatingType { get; }
    }
}