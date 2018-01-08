// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using Moq;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.Helpers
{
    public static class ValidationContextHelpers
    {
        public static void Mock(
            this Mock<IValidationEntitiesContext> validationContext,
            Mock<IDbSet<ValidatorStatus>> validatorStatusesMock = null,
            Mock<IDbSet<PackageSigningState>> packageSigningStatesMock = null,
            Mock<IDbSet<PackageSignature>> packageSignaturesMock = null,
            Mock<IDbSet<TrustedTimestamp>> trustedTimestampsMock = null,
            Mock<IDbSet<EndCertificate>> endCertificatesMock = null,
            Mock<IDbSet<EndCertificateValidation>> certificateValidationsMock = null,
            IEnumerable<ValidatorStatus> validatorStatuses = null,
            IEnumerable<PackageSigningState> packageSigningStates = null,
            IEnumerable<PackageSignature> packageSignatures = null,
            IEnumerable<TrustedTimestamp> trustedTimestamps = null,
            IEnumerable<EndCertificate> endCertificates = null,
            IEnumerable<EndCertificateValidation> certificateValidations = null)
        {
            validationContext.SetupDbSet(c => c.ValidatorStatuses, validatorStatusesMock, validatorStatuses);
            validationContext.SetupDbSet(c => c.PackageSigningStates, packageSigningStatesMock, packageSigningStates);
            validationContext.SetupDbSet(c => c.PackageSignatures, packageSignaturesMock, packageSignatures);
            validationContext.SetupDbSet(c => c.TrustedTimestamps, trustedTimestampsMock, trustedTimestamps);
            validationContext.SetupDbSet(c => c.EndCertificates, endCertificatesMock, endCertificates);
            validationContext.SetupDbSet(c => c.CertificateValidations, certificateValidationsMock, certificateValidations);
        }

        private static void SetupDbSet<T>(
            this Mock<IValidationEntitiesContext> validationContext,
            Expression<Func<IValidationEntitiesContext, IDbSet<T>>> propertyExpression,
            Mock<IDbSet<T>> dbSet,
            IEnumerable<T> dataEnumerable)
          where T : class
        {
            dbSet = dbSet ?? new Mock<IDbSet<T>>();
            dataEnumerable = dataEnumerable ?? new T[0];

            var data = dataEnumerable.AsQueryable();

            dbSet
                .As<IDbAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator())
                .Returns(new TestDbAsyncEnumerator<T>(data.GetEnumerator()));

            dbSet
                .As<IQueryable<T>>()
                .Setup(m => m.Provider)
                .Returns(new TestDbAsyncQueryProvider<T>(data.Provider));

            dbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
            dbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
            dbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());

            validationContext.Setup(propertyExpression).Returns(dbSet.Object);
        }
    }
}
