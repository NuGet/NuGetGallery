// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGet.Services.Validation;
using NuGet.Versioning;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminRevalidatePackageRequest : IValidatableObject
    {
        [Required]
        public List<AdminRevalidatePackageEntry> Packages { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string Reason { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Packages == null)
            {
                yield break;
            }

            if (Packages.Count == 0)
            {
                yield return new ValidationResult("The packages field must contain at least one entry.", [nameof(Packages)]);
            }

            if (Packages.Count > AdminRequestLimits.MaxPackageCount)
            {
                yield return new ValidationResult($"The packages field must contain at most {AdminRequestLimits.MaxPackageCount} entries.", [nameof(Packages)]);
            }

            for (var i = 0; i < Packages.Count; i++)
            {
                if (Packages[i] == null)
                {
                    yield return new ValidationResult("Package entry must not be null.", [$"{nameof(Packages)}[{i}]"]);
                }
            }
        }
    }

    public class AdminRevalidatePackageEntry : IValidatableObject
    {
        [Required(AllowEmptyStrings = false)]
        public string Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string Version { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string ValidatingType { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrWhiteSpace(Version) && !NuGetVersion.TryParse(Version, out _))
            {
                yield return new ValidationResult("The version field must be a valid NuGet version.", [nameof(Version)]);
            }

            if (!string.IsNullOrWhiteSpace(ValidatingType)
                && ValidatingType != nameof(NuGet.Services.Validation.ValidatingType.Package)
                && ValidatingType != nameof(NuGet.Services.Validation.ValidatingType.SymbolPackage))
            {
                yield return new ValidationResult(
                    $"The validatingType field must be '{nameof(NuGet.Services.Validation.ValidatingType.Package)}' or '{nameof(NuGet.Services.Validation.ValidatingType.SymbolPackage)}'.",
                    [nameof(ValidatingType)]);
            }
        }
    }
}
