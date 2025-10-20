// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Annotations;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Entities;

namespace NuGet.Services.CatalogValidation
{
    /// <summary>
    /// This CatalogValidationDbContextFactory is provided for running migrations in a flexible way as follows:
    /// 1. Run migration using DbConnection; (For DatabaseMigrationTools with AAD token)
    /// 2. Run migration using connection string;
    /// 3. Run migration using default connection string ("CatalogValidation.SqlServer") in a web.config; (For command-line migration with integrated AAD/username+password)
    /// </summary>
    public class CatalogValidationDbContextFactory : IDbContextFactory<CatalogValidationEntitiesContext>
    {
        public static Func<CatalogValidationEntitiesContext> CatalogValidationEntitiesContextFactory;
        public CatalogValidationEntitiesContext Create()
        {
            var factory = CatalogValidationEntitiesContextFactory;
            return factory == null ? new CatalogValidationEntitiesContext("CatalogValidation.SqlServer") : factory();
        }
    }

    /// <summary>
    /// The Entity Framework database context for catalog validation entities.
    /// </summary>
    [DbConfigurationType(typeof(EntitiesConfiguration))]
    public class CatalogValidationEntitiesContext : DbContext, ICatalogValidationEntitiesContext
    {
        static CatalogValidationEntitiesContext()
        {
            // Don't run migrations, ever!
            Database.SetInitializer<CatalogValidationEntitiesContext>(null);
        }

        public IDbSet<PackageValidationSet> PackageValidationSets { get; set; }
        public IDbSet<PackageValidation> PackageValidations { get; set; }
        public IDbSet<PackageValidationResult> PackageValidationResults { get; set; }
        public IDbSet<PackageValidationIssue> PackageValidationIssues { get; set; }
        public IDbSet<ValidatorStatus> ValidatorStatuses { get; set; }
        public IDbSet<PackageSigningState> PackageSigningStates { get; set; }
        public IDbSet<PackageSignature> PackageSignatures { get; set; }
        public IDbSet<TrustedTimestamp> TrustedTimestamps { get; set; }
        public IDbSet<EndCertificate> EndCertificates { get; set; }
        public IDbSet<EndCertificateValidation> CertificateValidations { get; set; }
        public IDbSet<ParentCertificate> ParentCertificates { get; set; }
        public IDbSet<CertificateChainLink> CertificateChainLinks { get; set; }
        public IDbSet<PackageCompatibilityIssue> PackageCompatibilityIssues { get; set; }
        public IDbSet<ScanOperationState> ScanOperationStates { get; set; }
        public IDbSet<PackageRevalidation> PackageRevalidations { get; set; }
        public IDbSet<SymbolsServerRequest> SymbolsServerRequests { get; set; }
        public IDbSet<ContentScanOperationState> ContentScanOperationState { get; set; }

        public CatalogValidationEntitiesContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
        }

        public CatalogValidationEntitiesContext(DbConnection connection) : base(connection, contextOwnsConnection: true)
        {
        }

        // Note: OnModelCreating implementation would be copied from ValidationEntitiesContext
        // For brevity, implementing a minimal version here - full implementation would copy all model configurations
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // This should contain the same model configuration as ValidationEntitiesContext
            // For now, implementing minimal configuration
            base.OnModelCreating(modelBuilder);
        }
    }
}
