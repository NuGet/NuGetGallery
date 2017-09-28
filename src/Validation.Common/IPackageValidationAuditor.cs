// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common
{
    /// <summary>
    /// The interface for reading package audit information. Only the VCS validation is initiated using this interface.
    /// </summary>
    public interface IPackageValidationAuditor
    {
        /// <summary>
        /// Reads the validation audit information. The three parameters comprise the audit key.
        /// </summary>
        /// <param name="validationId">The validation ID.</param>
        /// <param name="packageId">The package ID.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <returns>The package audit information. <code>null</code> if the audit does not exist.</returns>
        Task<PackageValidationAudit> ReadAuditAsync(Guid validationId, string packageId, string packageVersion);
    }
}