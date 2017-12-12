// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        Task<int> SaveChangesAsync();
    }
}
