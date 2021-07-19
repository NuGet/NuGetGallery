// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Validation.Entities;
using System.Data.Entity;
using System.Threading.Tasks;

namespace NuGet.Services.Validation
{
    public interface IValidationEntitiesContext
    {
        IDbSet<PackageValidationSet> PackageValidationSets { get; }
        IDbSet<PackageValidation> PackageValidations { get; }
        IDbSet<PackageValidationIssue> PackageValidationIssues { get; }
        IDbSet<ValidatorStatus> ValidatorStatuses { get; }
        IDbSet<PackageSigningState> PackageSigningStates { get; }
        IDbSet<PackageSignature> PackageSignatures { get; }
        IDbSet<TrustedTimestamp> TrustedTimestamps { get; }
        IDbSet<EndCertificate> EndCertificates { get; }
        IDbSet<EndCertificateValidation> CertificateValidations { get; }
        IDbSet<CertificateChainLink> CertificateChainLinks { get; }
        IDbSet<ParentCertificate> ParentCertificates { get; }
        IDbSet<PackageCompatibilityIssue> PackageCompatibilityIssues { get; }
        IDbSet<ScanOperationState> ScanOperationStates { get; }
        IDbSet<PackageRevalidation> PackageRevalidations { get; set; }
        IDbSet<SymbolsServerRequest> SymbolsServerRequests { get; set; }
        IDbSet<ContentScanOperationState> ContentScanOperationState { get; set; }

        Task<int> SaveChangesAsync();
    }
}
