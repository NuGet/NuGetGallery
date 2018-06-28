using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public static class PackageValidationSetExtensions
    {
        public static IReadOnlyList<ValidationIssue> GetValidationIssues(this PackageValidationSet validationSet)
        {
            IReadOnlyList<ValidationIssue> issues = null;

            if (validationSet != null)
            {
                // Get the failed validation set's validation issues. The issues are ordered by their
                // key so that it appears that issues are appended as more validations fail.
                issues = validationSet
                    .PackageValidations
                    .SelectMany(v => v.PackageValidationIssues)
                    .OrderBy(i => i.Key)
                    .Select(i => ValidationIssue.Deserialize(i.IssueCode, i.Data))
                    .ToList();

                // Filter out unknown issues and deduplicate the issues by code and data. This also deduplicates cases
                // where there is extraneous data in the serialized data field or if the issue code is unknown.
                issues = issues
                    .GroupBy(x => new { x.IssueCode, Data = x.Serialize() })
                    .Select(x => x.First())
                    .ToList();
            }

            // If the package failed validation but we could not find an issue that explains why, use a generic error message.
            if (issues == null || !issues.Any())
            {
                issues = new[] { ValidationIssue.Unknown };
            }

            return issues;
        }
    }
}
