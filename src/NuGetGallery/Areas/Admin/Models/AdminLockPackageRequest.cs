// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminLockPackageRequest : IValidatableObject
    {
        public List<AdminLockPackageIdentity> Packages { get; set; }

        [Required(ErrorMessage = "The locked field is required.")]
        public bool? Locked { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "The reason field is required.")]
        public string Reason { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Packages == null)
            {
                yield return new ValidationResult("The packages field is required.", [nameof(Packages)]);
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

    public class AdminLockPackageIdentity
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "The package id field is required.")]
        public string Id { get; set; }
    }
}
