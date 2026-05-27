using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGet.Versioning;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminSoftDeletePackageRequest : IValidatableObject
    {
        public List<AdminSoftDeletePackageIdentity> Packages { get; set; }

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

    public class AdminSoftDeletePackageIdentity : IValidatableObject
    {
        public const string AllVersionsWildcard = "*";

        [Required(AllowEmptyStrings = false, ErrorMessage = "The package id field is required.")]
        public string Id { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "The package version field is required. Use \"*\" to target all versions.")]
        public string Version { get; set; }

        public bool IsAllVersions => string.Equals(Version?.Trim(), AllVersionsWildcard, System.StringComparison.Ordinal);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrWhiteSpace(Version) && !IsAllVersions && !NuGetVersion.TryParse(Version, out _))
            {
                yield return new ValidationResult("The package version field must be a valid NuGet version or \"*\" for all versions.", [nameof(Version)]);
            }
        }
    }
}
