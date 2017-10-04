// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.Threading.Tasks;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The Entity Framework database context for validation entities.
    /// </summary>
    [DbConfigurationType(typeof(EntitiesConfiguration))]
    public class ValidationEntitiesContext : DbContext, IValidationEntitiesContext
    {
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
        private const string PackageSignaturesStatusIndex = "IX_PackageSignatures_Status";

        private const string CertificatesTable = "Certificates";
        private const string CertificatesThumbprintIndex = "IX_Certificates_Thumbprint";

        private const string CertificateValidationsTable = "CertificateValidations";
        private const string CertificateValidationsValidationIdIndex = "IX_CertificateValidations_ValidationId";
        private const string CertificateValidationsCertificateKeyValidationIdIndex = "IX_CertificateValidations_CertificateKey_ValidationId";

        static ValidationEntitiesContext()
        {
            // Don't run migrations, ever!
            Database.SetInitializer<ValidationEntitiesContext>(null);
        }

        public IDbSet<PackageValidationSet> PackageValidationSets { get; set; }
        public IDbSet<PackageValidation> PackageValidations { get; set; }
        public IDbSet<ValidatorStatus> ValidatorStatuses { get; set; }

        public IDbSet<PackageSigningState> PackageSigningStates { get; set; }
        public IDbSet<PackageSignature> PackageSignatures { get; set; }
        public IDbSet<Certificate> Certificates { get; set; }
        public IDbSet<CertificateValidation> CertificateValidations { get; set; }

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
                .Property(s => s.SignedAt)
                .HasColumnType("datetime2");

            modelBuilder.Entity<PackageSignature>()
                .Property(s => s.CreatedAt)
                .HasColumnType("datetime2")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<PackageSignature>()
                .HasMany(s => s.Certificates)
                .WithMany(c => c.PackageSignatures)
                .Map(m =>
                {
                    m.MapLeftKey("PackageSignatureKey");
                    m.MapRightKey("CertificateKey");
                    m.ToTable("PackageSignatureCertificates", SignatureSchema);
                });

            modelBuilder.Entity<Certificate>()
                .ToTable(CertificatesTable, SignatureSchema)
                .HasKey(c => c.Key);

            modelBuilder.Entity<Certificate>()
                .Property(c => c.Thumbprint)
                .HasMaxLength(20)
                .HasColumnType("char")
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(CertificatesThumbprintIndex)
                        {
                            IsUnique = true,
                        }
                    }));

            modelBuilder.Entity<Certificate>()
                .Property(c => c.StatusUpdateTime)
                .HasColumnType("datetime2");

            modelBuilder.Entity<Certificate>()
                .Property(c => c.NextStatusUpdateTime)
                .HasColumnType("datetime2");

            modelBuilder.Entity<Certificate>()
                .Property(c => c.LastVerificationTime)
                .HasColumnType("datetime2");

            modelBuilder.Entity<Certificate>()
                .Property(c => c.RevocationTime)
                .HasColumnType("datetime2");

            modelBuilder.Entity<Certificate>()
                .HasMany(c => c.Validations)
                .WithRequired(v => v.Certificate)
                .HasForeignKey(v => v.CertificateKey)
                .WillCascadeOnDelete();

            modelBuilder.Entity<CertificateValidation>()
                .ToTable(CertificateValidationsTable, SignatureSchema)
                .HasKey(v => v.Key);

            modelBuilder.Entity<CertificateValidation>()
                .Property(v => v.CertificateKey)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(CertificateValidationsCertificateKeyValidationIdIndex, 1)
                    }));

            modelBuilder.Entity<CertificateValidation>()
                .Property(v => v.ValidationId)
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(CertificateValidationsValidationIdIndex),
                        new IndexAttribute(CertificateValidationsCertificateKeyValidationIdIndex, 2)
                    }));
        }
    }
}
