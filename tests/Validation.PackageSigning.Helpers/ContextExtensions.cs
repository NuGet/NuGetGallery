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
using NuGetGallery;

namespace Tests.ContextHelpers
{
    using GalleryContext = NuGetGallery.IEntitiesContext;

    public static class ContextExtensions
    {
        public static void Mock(
            this Mock<GalleryContext> context,
            Mock<IDbSet<PackageRegistration>> packageRegistrationsMock = null,
            Mock<IDbSet<PackageDependency>> packageDependenciesMock = null,
            Mock<IDbSet<Package>> packagesMock = null,
            IEnumerable<PackageRegistration> packageRegistrations = null,
            IEnumerable<PackageDependency> packageDependencies = null,
            IEnumerable<Package> packages = null)
        {
            context.SetupDbSet(c => c.PackageRegistrations, packageRegistrationsMock, packageRegistrations);
            context.SetupDbSet(c => c.Set<PackageDependency>(), packageDependenciesMock, packageDependencies);
            context.SetupDbSet(c => c.Set<Package>(), packagesMock, packages);
        }

        public static void Mock(
            this Mock<IValidationEntitiesContext> validationContext,
            Mock<IDbSet<ValidatorStatus>> validatorStatusesMock = null,
            Mock<IDbSet<PackageSigningState>> packageSigningStatesMock = null,
            Mock<IDbSet<PackageSignature>> packageSignaturesMock = null,
            Mock<IDbSet<TrustedTimestamp>> trustedTimestampsMock = null,
            Mock<IDbSet<EndCertificate>> endCertificatesMock = null,
            Mock<IDbSet<EndCertificateValidation>> certificateValidationsMock = null,
            Mock<IDbSet<PackageRevalidation>> packageRevalidationsMock = null,
            IEnumerable<ValidatorStatus> validatorStatuses = null,
            IEnumerable<PackageSigningState> packageSigningStates = null,
            IEnumerable<PackageSignature> packageSignatures = null,
            IEnumerable<TrustedTimestamp> trustedTimestamps = null,
            IEnumerable<EndCertificate> endCertificates = null,
            IEnumerable<EndCertificateValidation> certificateValidations = null,
            IEnumerable<PackageRevalidation> packageRevalidations = null)
        {
            validationContext.SetupDbSet(c => c.ValidatorStatuses, validatorStatusesMock, validatorStatuses);
            validationContext.SetupDbSet(c => c.PackageSigningStates, packageSigningStatesMock, packageSigningStates);
            validationContext.SetupDbSet(c => c.PackageSignatures, packageSignaturesMock, packageSignatures);
            validationContext.SetupDbSet(c => c.TrustedTimestamps, trustedTimestampsMock, trustedTimestamps);
            validationContext.SetupDbSet(c => c.EndCertificates, endCertificatesMock, endCertificates);
            validationContext.SetupDbSet(c => c.CertificateValidations, certificateValidationsMock, certificateValidations);
            validationContext.SetupDbSet(c => c.PackageRevalidations, packageRevalidationsMock, packageRevalidations);
        }

        private static void SetupDbSet<TContext, TEntity>(
            this Mock<TContext> validationContext,
            Expression<Func<TContext, IDbSet<TEntity>>> dbSetAccessor,
            Mock<IDbSet<TEntity>> dbSet,
            IEnumerable<TEntity> dataEnumerable)
          where TContext : class
          where TEntity : class
        {
            dbSet = dbSet ?? new Mock<IDbSet<TEntity>>();
            dataEnumerable = dataEnumerable ?? new TEntity[0];

            var data = dataEnumerable.AsQueryable();

            dbSet
                .As<IDbAsyncEnumerable<TEntity>>()
                .Setup(m => m.GetAsyncEnumerator())
                .Returns(() => new TestDbAsyncEnumerator<TEntity>(data.GetEnumerator()));

            dbSet
                .As<IQueryable<TEntity>>()
                .Setup(m => m.Provider)
                .Returns(() => new TestDbAsyncQueryProvider<TEntity>(data.Provider));

            dbSet
                .Setup(s => s.Add(It.IsAny<TEntity>()))
                .Callback<TEntity>(e => data = data.Concat(new[] { e }).AsQueryable());

            dbSet
                .Setup(s => s.Remove(It.IsAny<TEntity>()))
                .Callback<TEntity>(e => data = data.Where(d => e != d).AsQueryable());

            dbSet.As<IQueryable<TEntity>>().Setup(m => m.Expression).Returns(() => data.Expression);
            dbSet.As<IQueryable<TEntity>>().Setup(m => m.ElementType).Returns(() => data.ElementType);
            dbSet.As<IQueryable<TEntity>>().Setup(m => m.GetEnumerator()).Returns(() => data.GetEnumerator());

            validationContext.Setup(dbSetAccessor).Returns(dbSet.Object);
        }
    }
}
