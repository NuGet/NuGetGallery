// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGet.Versioning;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminUpdateListedPackageRequest : IValidatableObject
    {
        [Required]
        public List<AdminUpdateListedPackageIdentity> Packages { get; set; }

        [Required]
        public bool? Listed { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string Reason { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Packages is null)
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
                if (Packages[i] is null)
                {
                    yield return new ValidationResult("Package entry must not be null.", [$"{nameof(Packages)}[{i}]"]);
                }
            }
        }
    }

    public class AdminUpdateListedPackageIdentity : IValidatableObject
    {
        [Required(AllowEmptyStrings = false)]
        public string Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string Version { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrWhiteSpace(Version) && !NuGetVersion.TryParse(Version, out _))
            {
                yield return new ValidationResult("The version field must be a valid NuGet version.", [nameof(Version)]);
            }
        }
    }
}
