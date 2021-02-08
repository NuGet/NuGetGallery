// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Validation;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class NuGetPackageValidationViewModel : IPackageVersionModel
    {
        public NuGetPackageValidationViewModel(
            IReadOnlyList<PackageValidationSet> validationSets,
            PackageDeletedStatus deletedStatus,
            ValidatingType validatingType)
        {
            if (validatingType == ValidatingType.Generic)
            {
                throw new ArgumentException(
                    $"Unsupported validation type of {validatingType}",
                    nameof(validatingType));
            }

            var first = validationSets.First();
            PackageKey = first.PackageKey.Value;
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