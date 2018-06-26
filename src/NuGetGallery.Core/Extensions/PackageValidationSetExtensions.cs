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
            // Get the failed validation set's validation issues. The issues are ordered by their
            // key so that it appears that issues are appended as more validations fail.
            var issues = validationSet
                .PackageValidations
                .SelectMany(v => v.PackageValidationIssues)
                .OrderBy(i => i.Key)
                .Select(i => ValidationIssue.Deserialize(i.IssueCode, i.Data))
                .ToList();

            // Filter out unknown issues and deduplicate the issues by code and data. This also deduplicates cases
            // where there is extraneous data in the serialized data field or if the issue code is unknown.
            return issues
                .GroupBy(x => new { x.IssueCode, Data = x.Serialize() })
                .Select(x => x.First())
                .ToList();
        }
    }
}
