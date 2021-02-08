// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq.Expressions;
using Moq;

namespace NuGet.Services.Validation
{
    public static class IValidationEntitiesContextExtensions
    {
        public static void Mock(
            this Mock<IValidationEntitiesContext> validationContext,
            Mock<IDbSet<PackageValidationSet>> packageValidationSetsMock = null,
            Mock<IDbSet<PackageValidation>> packageValidationsMock = null,
            Mock<IDbSet<ValidatorStatus>> validatorStatusesMock = null,
            Mock<IDbSet<PackageSigningState>> packageSigningStatesMock = null,
            Mock<IDbSet<PackageSignature>> packageSignaturesMock = null,
            Mock<IDbSet<TrustedTimestamp>> trustedTimestampsMock = null,
            Mock<IDbSet<EndCertificate>> endCertificatesMock = null,
            Mock<IDbSet<EndCertificateValidation>> certificateValidationsMock = null,
            Mock<IDbSet<PackageRevalidation>> packageRevalidationsMock = null,
            Mock<IDbSet<ParentCertificate>> parentCertificatesMock = null,
            Mock<IDbSet<CertificateChainLink>> certificateChainLinksMock = null,
            IEnumerable<PackageValidationSet> packageValidationSets = null,
            IEnumerable<PackageValidation> packageValidations = null,
            IEnumerable<ValidatorStatus> validatorStatuses = null,
            IEnumerable<PackageSigningState> packageSigningStates = null,
            IEnumerable<PackageSignature> packageSignatures = null,
            IEnumerable<TrustedTimestamp> trustedTimestamps = null,
            IEnumerable<EndCertificate> endCertificates = null,
            IEnumerable<EndCertificateValidation> certificateValidations = null,
            IEnumerable<PackageRevalidation> packageRevalidations = null,
            IEnumerable<ParentCertificate> parentCertificates = null,
            IEnumerable<CertificateChainLink> certificateChainLinks = null)
        {
            validationContext.SetupDbSet(c => c.PackageValidationSets, packageValidationSetsMock, packageValidationSets);
            validationContext.SetupDbSet(c => c.PackageValidations, packageValidationsMock, packageValidations);
            validationContext.SetupDbSet(c => c.ValidatorStatuses, validatorStatusesMock, validatorStatuses);
            validationContext.SetupDbSet(c => c.PackageSigningStates, packageSigningStatesMock, packageSigningStates);
            validationContext.SetupDbSet(c => c.PackageSignatures, packageSignaturesMock, packageSignatures);
            validationContext.SetupDbSet(c => c.TrustedTimestamps, trustedTimestampsMock, trustedTimestamps);
            validationContext.SetupDbSet(c => c.EndCertificates, endCertificatesMock, endCertificates);
            validationContext.SetupDbSet(c => c.CertificateValidations, certificateValidationsMock, certificateValidations);
            validationContext.SetupDbSet(c => c.PackageRevalidations, packageRevalidationsMock, packageRevalidations);
            validationContext.SetupDbSet(c => c.ParentCertificates, parentCertificatesMock, parentCertificates);
            validationContext.SetupDbSet(c => c.CertificateChainLinks, certificateChainLinksMock, certificateChainLinks);
        }
    }
}
