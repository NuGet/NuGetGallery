// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The Entity Framework database context for validation entities.
    /// </summary>
    [DbConfigurationType(typeof(EntitiesConfiguration))]
    public class ValidationEntitiesContext : DbContext, IValidationEntitiesContext
    {
        /// <summary>
        /// We use a SHA-256 thumbprint for comparing certificates. This has a digest size of 256 bytes which is 64
        /// characters long when encoding as a hexadecimal string. However, to be flexible for future hash algorithms,
        /// we take a larger value so that no schema change will be necessary (hopefully).
        /// </summary>
        private const int MaximumThumbprintLength = 256;

        /// <summary>
        /// Since we encode thumbprints using hexadecimal, NVARCHAR is not necessary. Additionally, we use varchar
        /// instead of char so that hash algorithm changes do no require schema changes.
        /// </summary>
        private const string ThumbprintColumnType = "varchar";

        private const string SignatureSchema = "signature";

        private const string PackageValidationSetsValidationTrackingId = "IX_PackageValidationSets_ValidationTrackingId";
        private const string PackageValidationSetsPackageKeyIndex = "IX_PackageValidationSets_PackageKey";
        private const string PackageValidationSetsPackageIdPackageVersionIndex = "IX_PackageValidationSets_PackageId_PackageNormalizedVersion";

        private const string ValidatorStatusesTable = "ValidatorStatuses";
        private const string ValidatorStatusesPackageKeyIndex = "IX_ValidatorStatuses_PackageKey";
        
        private const string PackageSigningStatesTable = "PackageSigningStates";
        private const string PackageSigningStatesPackageIdPackageVersionIndex = "IX_PackageSigningStates_PackageId_PackageNormalizedVersion";

        private const string PackageSignaturesTable = "PackageSignatures";
        private const string PackageSignaturesPackageKeyIndex = "IX_PackageSignatures_PackageKey";
        private const string PackageSignaturesEndCertificateKeyIndex = "IX_PackageSignatures_EndCertificateKey";
        private const string PackageSignaturesStatusIndex = "IX_PackageSignatures_Status";

        private const string TrustedTimestampsTable = "TrustedTimestamps";
        private const string TrustedTimestampsPackageSignatureKeyIndex = "IX_TrustedTimestamps_PackageSignatureKey";

        private const string EndCertificatesTable = "EndCertificates";
        private const string EndCertificatesThumbprintIndex = "IX_EndCertificates_Thumbprint";

        private const string ParentCertificatesTable = "ParentCertificates";
        private const string ParentCertificatesThumbprintIndex = "IX_ParentCertificates_Thumbprint";

        private const string CertificateChainLinksTable = "CertificateChainLinks";
        private const string CertificateChainLinkEndCertificateKeyParentCertificateKeyIndex = "IX_CertificateChainLinks_EndCertificateKeyParentCertificateKey";

        private const string EndCertificateValidationsTable = "EndCertificateValidations";
        private const string EndCertificateValidationsValidationIdIndex = "IX_EndCertificateValidations_ValidationId";
        private const string EndCertificateValidationsCertificateKeyValidationIdIndex = "IX_EndCertificateValidations_EndCertificateKey_ValidationId";

        private const string PackageCompatibilityIssuesTable = "PackageCompatibilityIssues";

        static ValidationEntitiesContext()
        {
            // Don't run migrations, ever!
            Database.SetInitializer<ValidationEntitiesContext>(null);
        }

        public IDbSet<PackageValidationSet> PackageValidationSets { get; set; }
        public IDbSet<PackageValidation> PackageValidations { get; set; }
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

        public ValidationEntitiesContext() : this("Validation.SqlServer")
        {
        }

        public ValidationEntitiesContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PackageValidationSet>()
                .HasKey(pvs => pvs.Key);

            modelBuilder.Entity<PackageValidationSet>()
                .Property(pvs => pvs.ValidationTrackingId)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageValidationSetsValidationTrackingId)
                        {
                            IsUnique = true
                        }
                    }));

            modelBuilder.Entity<PackageValidationSet>()
                .Property(pvs => pvs.PackageKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageValidationSetsPackageKeyIndex)
                    }));

            modelBuilder.Entity<PackageValidationSet>()
                .Property(pvs => pvs.PackageId)
                .HasMaxLength(128)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageValidationSetsPackageIdPackageVersionIndex, 1)
                    }));

            modelBuilder.Entity<PackageValidationSet>()
                .Property(pvs => pvs.PackageNormalizedVersion)
                .HasMaxLength(64)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageValidationSetsPackageIdPackageVersionIndex, 2)
                    }));

            modelBuilder.Entity<PackageValidationSet>()
                .Property(pvs => pvs.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<PackageValidationSet>()
                .Property(pvs => pvs.Created)
                .HasColumnType("datetime2");

            modelBuilder.Entity<PackageValidationSet>()
                .Property(pvs => pvs.Updated)
                .HasColumnType("datetime2");

            modelBuilder.Entity<PackageValidation>()
                .HasKey(pv => pv.Key);

            modelBuilder.Entity<PackageValidation>()
                .Property(pv => pv.Key)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<PackageValidation>()
                .Property(pv => pv.Type)
                .HasMaxLength(255)
                .HasColumnType("varchar")
                .IsRequired();

            modelBuilder.Entity<PackageValidation>()
                .Property(pv => pv.Started)
                .IsOptional()
                .HasColumnType("datetime2");

            modelBuilder.Entity<PackageValidation>()
                .Property(pv => pv.ValidationStatusTimestamp)
                .HasColumnType("datetime2");

            modelBuilder.Entity<PackageValidation>()
                .Property(pv => pv.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<PackageValidationIssue>()
                .HasKey(e => e.Key);

            modelBuilder.Entity<PackageValidationIssue>()
                .Property(pv => pv.Key)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<PackageValidationIssue>()
                .Property(pv => pv.Data)
                .IsRequired();

            modelBuilder.Entity<PackageCompatibilityIssue>()
                .HasKey(e => e.Key);

            modelBuilder.Entity<PackageCompatibilityIssue>()
                .Property(pv => pv.Key)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<PackageCompatibilityIssue>()
                .Property(pv => pv.Message)
                .IsRequired();

            modelBuilder.Entity<PackageCompatibilityIssue>()
                .Property(pv => pv.ClientIssueCode)
                .IsRequired();

            modelBuilder.Entity<PackageCompatibilityIssue>()
                .ToTable(PackageCompatibilityIssuesTable)
                .HasKey(s => s.Key);

            modelBuilder.Entity<ValidatorStatus>()
                .ToTable(ValidatorStatusesTable)
                .HasKey(s => s.ValidationId);

            modelBuilder.Entity<ValidatorStatus>()
                .Property(s => s.ValidationId)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);

            modelBuilder.Entity<ValidatorStatus>()
                .Property(s => s.PackageKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(ValidatorStatusesPackageKeyIndex)
                    }));

            modelBuilder.Entity<ValidatorStatus>()
                .Property(s => s.ValidatorName)
                .IsRequired();

            modelBuilder.Entity<ValidatorStatus>()
                .Property(r => r.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<ValidatorIssue>()
                .HasKey(vi => vi.Key);

            modelBuilder.Entity<ValidatorIssue>()
                .Property(vi => vi.Key)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<ValidatorIssue>()
                .Property(vi => vi.Data)
                .IsRequired();

            RegisterPackageSigningEntities(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        private void RegisterPackageSigningEntities(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PackageSigningState>()
                .ToTable(PackageSigningStatesTable, SignatureSchema)
                .HasKey(p => p.PackageKey);

            modelBuilder.Entity<PackageSigningState>()
                .Property(p => p.PackageKey)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);

            modelBuilder.Entity<PackageSigningState>()
                .Property(p => p.PackageId)
                .HasMaxLength(128)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageSigningStatesPackageIdPackageVersionIndex, 1)
                    }));

            modelBuilder.Entity<PackageSigningState>()
                .Property(p => p.PackageNormalizedVersion)
                .HasMaxLength(64)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageSigningStatesPackageIdPackageVersionIndex, 2)
                    }));

            modelBuilder.Entity<PackageSigningState>()
                .HasMany(p => p.PackageSignatures)
                .WithRequired(s => s.PackageSigningState)
                .HasForeignKey(s => s.PackageKey)
                .WillCascadeOnDelete();

            modelBuilder.Entity<PackageSignature>()
                .ToTable(PackageSignaturesTable, SignatureSchema)
                .HasKey(s => s.Key);

            modelBuilder.Entity<PackageSignature>()
                .Property(s => s.Key)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<PackageSignature>()
                .Property(s => s.PackageKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageSignaturesPackageKeyIndex)
                        {
                            IsUnique = true,
                        }
                    }));

            modelBuilder.Entity<PackageSignature>()
                .Property(s => s.EndCertificateKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageSignaturesEndCertificateKeyIndex)
                    }));

            modelBuilder.Entity<PackageSignature>()
                .Property(s => s.Status)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(PackageSignaturesStatusIndex)
                    }));

            modelBuilder.Entity<PackageSignature>()
                .Property(s => s.CreatedAt)
                .HasColumnType("datetime2");

            modelBuilder.Entity<PackageSignature>()
                .Property(c => c.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<PackageSignature>()
                .HasMany(s => s.TrustedTimestamps)
                .WithRequired(t => t.PackageSignature)
                .HasForeignKey(t => t.PackageSignatureKey)
                .WillCascadeOnDelete();

            modelBuilder.Entity<PackageSignature>()
                .HasRequired(s => s.EndCertificate)
                .WithMany(c => c.PackageSignatures)
                .HasForeignKey(s => s.EndCertificateKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TrustedTimestamp>()
                .ToTable(TrustedTimestampsTable, SignatureSchema)
                .HasKey(t => t.Key);

            modelBuilder.Entity<TrustedTimestamp>()
                .Property(t => t.Value)
                .HasColumnType("datetime2");

            modelBuilder.Entity<TrustedTimestamp>()
                .HasRequired(s => s.EndCertificate)
                .WithMany(c => c.TrustedTimestamps)
                .HasForeignKey(s => s.EndCertificateKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TrustedTimestamp>()
                .Property(s => s.PackageSignatureKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(TrustedTimestampsPackageSignatureKeyIndex)
                        {
                            IsUnique = true,
                        }
                    }));

            modelBuilder.Entity<EndCertificate>()
                .ToTable(EndCertificatesTable, SignatureSchema)
                .HasKey(c => c.Key);

            modelBuilder.Entity<EndCertificate>()
                .Property(c => c.Thumbprint)
                .HasMaxLength(MaximumThumbprintLength)
                .HasColumnType(ThumbprintColumnType)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(EndCertificatesThumbprintIndex)
                        {
                            IsUnique = true,
                        }
                    }));

            modelBuilder.Entity<EndCertificate>()
                .Property(c => c.StatusUpdateTime)
                .HasColumnType("datetime2");

            modelBuilder.Entity<EndCertificate>()
                .Property(c => c.NextStatusUpdateTime)
                .HasColumnType("datetime2");

            modelBuilder.Entity<EndCertificate>()
                .Property(c => c.LastVerificationTime)
                .HasColumnType("datetime2");

            modelBuilder.Entity<EndCertificate>()
                .Property(c => c.RevocationTime)
                .HasColumnType("datetime2");

            modelBuilder.Entity<EndCertificate>()
                .Property(c => c.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<EndCertificate>()
                .HasMany(c => c.Validations)
                .WithRequired(v => v.EndCertificate)
                .HasForeignKey(v => v.EndCertificateKey)
                .WillCascadeOnDelete();

            modelBuilder.Entity<EndCertificate>()
                .HasMany(c => c.CertificateChainLinks)
                .WithRequired(v => v.EndCertificate)
                .HasForeignKey(v => v.EndCertificateKey)
                .WillCascadeOnDelete();

            modelBuilder.Entity<CertificateChainLink>()
                .ToTable(CertificateChainLinksTable, SignatureSchema)
                .HasKey(t => t.Key);

            modelBuilder.Entity<CertificateChainLink>()
                .Property(v => v.EndCertificateKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(CertificateChainLinkEndCertificateKeyParentCertificateKeyIndex, 0)
                        {
                            IsUnique = true
                        }
                    }));

            modelBuilder.Entity<CertificateChainLink>()
                .Property(v => v.ParentCertificateKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(CertificateChainLinkEndCertificateKeyParentCertificateKeyIndex, 1)
                        {
                            IsUnique = true
                        }
                    }));

            modelBuilder.Entity<ParentCertificate>()
                .ToTable(ParentCertificatesTable, SignatureSchema)
                .HasKey(c => c.Key);

            modelBuilder.Entity<ParentCertificate>()
                .HasMany(v => v.CertificateChainLinks)
                .WithRequired(v => v.ParentCertificate)
                .HasForeignKey(v => v.ParentCertificateKey)
                .WillCascadeOnDelete();

            modelBuilder.Entity<ParentCertificate>()
                .Property(c => c.Thumbprint)
                .HasMaxLength(MaximumThumbprintLength)
                .HasColumnType(ThumbprintColumnType)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(ParentCertificatesThumbprintIndex)
                        {
                            IsUnique = true
                        }
                    }));

            modelBuilder.Entity<EndCertificateValidation>()
                .ToTable(EndCertificateValidationsTable, SignatureSchema)
                .HasKey(v => v.Key);

            modelBuilder.Entity<EndCertificateValidation>()
                .Property(v => v.EndCertificateKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(EndCertificateValidationsCertificateKeyValidationIdIndex, 1)
                    }));

            modelBuilder.Entity<EndCertificateValidation>()
                .Property(v => v.ValidationId)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(EndCertificateValidationsValidationIdIndex),
                        new IndexAttribute(EndCertificateValidationsCertificateKeyValidationIdIndex, 2)
                    }));
        }
    }
}
