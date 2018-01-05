// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Issues
{
    /// <summary>
    /// The validation issue that is raised when signed packages are disallowed and the validated package is signed.
    /// </summary>
    public class PackageIsSigned : ValidationIssue
    {
        public override ValidationIssueCode IssueCode => ValidationIssueCode.PackageIsSigned;

        public override string GetMessage() => Strings.PackageIsSignedMessage;
    }
}
