// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Validation;

namespace NuGet.Services.CatalogValidation.Issues
{
    /// <summary>
    /// Used as the concrete type for all issues codes that don't have any associated data. Note that the JSON
    /// deserialization code path used for other issue types does not construct this type, thus there is no
    /// <see cref="Newtonsoft.Json.JsonConstructorAttribute"/>.
    /// </summary>
    public sealed class NoDataCatalogValidationIssue : CatalogValidationIssue
    {
        public NoDataCatalogValidationIssue(ValidationIssueCode issueCode)
        {
            IssueCode = issueCode;
        }

        public override ValidationIssueCode IssueCode { get; }
    }
}