using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGetGallery;

public partial class NuGetGalleryContext : DbContext
{
    public NuGetGalleryContext()
    {
    }

    public NuGetGalleryContext(DbContextOptions<NuGetGalleryContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Certificate> Certificates { get; set; }

    public virtual DbSet<Credential> Credentials { get; set; }

    public virtual DbSet<Package> Packages { get; set; }

    public virtual DbSet<PackageDependency> PackageDependencies { get; set; }

    public virtual DbSet<PackageDeprecation> PackageDeprecations { get; set; }

    public virtual DbSet<PackageFramework> PackageFrameworks { get; set; }

    public virtual DbSet<PackageRegistration> PackageRegistrations { get; set; }

    public virtual DbSet<PackageVulnerability> PackageVulnerabilities { get; set; }

    public virtual DbSet<ReservedNamespace> ReservedNamespaces { get; set; }

    public virtual DbSet<Scope> Scopes { get; set; }

    public virtual DbSet<SymbolPackage> SymbolPackages { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserCertificate> UserCertificates { get; set; }

    public virtual DbSet<UserSecurityPolicy> UserSecurityPolicies { get; set; }

    public virtual DbSet<VulnerablePackageVersionRange> VulnerablePackageVersionRanges { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=NuGetGallery");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountDelete>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.AccountDeletes");

            entity.Property(e => e.DeletedOn).HasColumnType("datetime");

            entity.HasOne(d => d.DeletedAccount).WithMany()
                .HasForeignKey(d => d.DeletedAccountKey)
                .HasConstraintName("FK_dbo.AccountDeletes_dbo.Users_DeletedAccountKey");

            entity.HasOne(d => d.DeletedBy).WithMany()
                .HasForeignKey(d => d.DeletedByKey)
                .HasConstraintName("FK_dbo.AccountDeletes_dbo.Users_DeletedByKey");
        });

        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.Certificates");

            entity.Property(e => e.Sha1Thumbprint)
                .IsRequired()
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Thumbprint)
                .IsRequired()
                .HasMaxLength(256)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Credential>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.Credentials");

            entity.Property(e => e.Created)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(256);
            entity.Property(e => e.Expires).HasColumnType("datetime");
            entity.Property(e => e.Identity).HasMaxLength(256);
            entity.Property(e => e.LastUsed).HasColumnType("datetime");
            entity.Property(e => e.TenantId).HasMaxLength(256);
            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(64);
            entity.Property(e => e.Value)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserKey)
                .HasConstraintName("FK_dbo.Credentials_dbo.Users_UserKey");
        });

        modelBuilder.Entity<EmailMessage>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.HasOne(d => d.FromUser).WithMany().HasForeignKey(d => d.FromUserKey);

            entity.HasOne(d => d.ToUser).WithMany()
                .HasForeignKey(d => d.ToUserKey)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(e => new { e.OrganizationKey, e.MemberKey }).HasName("PK_dbo.Memberships");

            entity.HasOne(d => d.Member).WithMany()
                .HasForeignKey(d => d.MemberKey)
                .HasConstraintName("FK_dbo.Memberships_dbo.Users_MemberKey");

            entity.HasOne(d => d.Organization).WithMany()
                .HasForeignKey(d => d.OrganizationKey)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_dbo.Memberships_dbo.Organizations_OrganizationKey");
        });

        modelBuilder.Entity<MembershipRequest>(entity =>
        {
            entity.HasKey(e => new { e.OrganizationKey, e.NewMemberKey }).HasName("PK_dbo.MembershipRequests");

            entity.Property(e => e.ConfirmationToken).IsRequired();
            entity.Property(e => e.RequestDate).HasColumnType("datetime");

            entity.HasOne(d => d.NewMember).WithMany()
                .HasForeignKey(d => d.NewMemberKey)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_dbo.MembershipRequests_dbo.Users_NewMemberKey");

            entity.HasOne(d => d.Organization).WithMany()
                .HasForeignKey(d => d.OrganizationKey)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_dbo.MembershipRequests_dbo.Organizations_OrganizationKey");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.Organizations");

            entity.Property(e => e.Key).ValueGeneratedNever();
        });

        modelBuilder.Entity<OrganizationMigrationRequest>(entity =>
        {
            entity.HasKey(e => e.NewOrganizationKey).HasName("PK_dbo.OrganizationMigrationRequests");

            entity.Property(e => e.NewOrganizationKey).ValueGeneratedNever();
            entity.Property(e => e.ConfirmationToken).IsRequired();
            entity.Property(e => e.RequestDate).HasColumnType("datetime");

            entity.HasOne(d => d.AdminUser).WithMany()
                .HasForeignKey(d => d.AdminUserKey)
                .HasConstraintName("FK_dbo.OrganizationMigrationRequests_dbo.Users_AdminUserKey");

            entity.HasOne(d => d.NewOrganization).WithOne()
                .HasForeignKey<OrganizationMigrationRequest>(d => d.NewOrganizationKey)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_dbo.OrganizationMigrationRequests_dbo.Users_NewOrganizationKey");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.ToTable(tb => tb.HasTrigger("LastEditedTrigger"));

            entity.Property(e => e.Created)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Hash)
                .IsRequired()
                .HasMaxLength(256);
            entity.Property(e => e.HashAlgorithm).HasMaxLength(10);
            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Language).HasMaxLength(20);
            entity.Property(e => e.LastEdited).HasColumnType("datetime");
            entity.Property(e => e.LastUpdated)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.LicenseExpression).HasMaxLength(500);
            entity.Property(e => e.MinClientVersion).HasMaxLength(44);
            entity.Property(e => e.NormalizedVersion).HasMaxLength(64);
            entity.Property(e => e.Published)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.RepositoryType).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.Version)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasOne(d => d.Certificate).WithMany()
                .HasForeignKey(d => d.CertificateKey)
                .HasConstraintName("FK_dbo.Packages_dbo.Certificates_CertificateKey");

            entity.HasOne(d => d.PackageRegistration).WithMany()
                .HasForeignKey(d => d.PackageRegistrationKey)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserKey);
        });

        modelBuilder.Entity<PackageAuthor>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.HasOne(d => d.Package).WithMany()
                .HasForeignKey(d => d.PackageKey)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<PackageDelete>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.PackageDeletes");

            entity.Property(e => e.DeletedOn).HasColumnType("datetime");
            entity.Property(e => e.Reason).IsRequired();
            entity.Property(e => e.Signature).IsRequired();

            entity.HasOne(d => d.DeletedBy).WithMany()
                .HasForeignKey(d => d.DeletedByKey)
                .HasConstraintName("FK_dbo.PackageDeletes_dbo.Users_DeletedByKey");
        });

        modelBuilder.Entity<PackageDependency>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.TargetFramework).HasMaxLength(256);
            entity.Property(e => e.VersionSpec).HasMaxLength(256);

            entity.HasOne(d => d.Package).WithMany()
                .HasForeignKey(d => d.PackageKey)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<PackageDeprecation>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.PackageDeprecations");

            entity.Property(e => e.DeprecatedOn)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.AlternatePackage).WithMany()
                .HasForeignKey(d => d.AlternatePackageKey)
                .HasConstraintName("FK_dbo.PackageDeprecations_dbo.Packages_AlternatePackageKey");

            entity.HasOne(d => d.AlternatePackageRegistration).WithMany()
                .HasForeignKey(d => d.AlternatePackageRegistrationKey)
                .HasConstraintName("FK_dbo.PackageDeprecations_dbo.PackageRegistrations_AlternatePackageRegistrationKey");

            entity.HasOne(d => d.DeprecatedByUser).WithMany()
                .HasForeignKey(d => d.DeprecatedByUserKey)
                .HasConstraintName("FK_dbo.PackageDeprecations_dbo.Users_DeprecatedByUserKey");

            entity.HasOne(d => d.Package).WithOne()
                .HasForeignKey<PackageDeprecation>(d => d.PackageKey)
                .HasConstraintName("FK_dbo.PackageDeprecations_dbo.Packages_PackageKey");
        });

        modelBuilder.Entity<PackageFramework>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.Property(e => e.TargetFramework).HasMaxLength(256);

            entity.HasOne(d => d.Package).WithMany().HasForeignKey("Package_Key");
        });

        modelBuilder.Entity<PackageHistory>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.Property(e => e.Hash).HasMaxLength(256);
            entity.Property(e => e.HashAlgorithm).HasMaxLength(10);
            entity.Property(e => e.LastUpdated).HasColumnType("datetime");
            entity.Property(e => e.Published).HasColumnType("datetime");
            entity.Property(e => e.Timestamp).HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(256);

            entity.HasOne(d => d.Package).WithMany().HasForeignKey(d => d.PackageKey);

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserKey)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PackageLicense>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.PackageLicenses");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(128);
        });

        modelBuilder.Entity<PackageLicenseReport>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.PackageLicenseReports");

            entity.Property(e => e.Comment).HasMaxLength(256);
            entity.Property(e => e.CreatedUtc).HasColumnType("datetime");
            entity.Property(e => e.ReportUrl)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasOne(d => d.Package).WithMany()
                .HasForeignKey(d => d.PackageKey)
                .HasConstraintName("FK_dbo.PackageLicenseReports_dbo.Packages_PackageKey");

            entity.HasMany(d => d.Licenses).WithMany(p => p.Reports)
                .UsingEntity<Dictionary<string, object>>(
                    "PackageLicenseReportLicense",
                    r => r.HasOne<PackageLicense>().WithMany()
                        .HasForeignKey("LicenseKey")
                        .HasConstraintName("FK_dbo.PackageLicenseReportLicenses_dbo.PackageLicenses_LicenseKey"),
                    l => l.HasOne<PackageLicenseReport>().WithMany()
                        .HasForeignKey("ReportKey")
                        .HasConstraintName("FK_dbo.PackageLicenseReportLicenses_dbo.PackageLicenseReports_ReportKey"),
                    j =>
                    {
                        j.HasKey("ReportKey", "LicenseKey").HasName("PK_dbo.PackageLicenseReportLicenses");
                        j.ToTable("PackageLicenseReportLicenses");
                        j.HasIndex(new[] { "LicenseKey" }, "IX_LicenseKey");
                        j.HasIndex(new[] { "ReportKey" }, "IX_ReportKey");
                    });
        });

        modelBuilder.Entity<PackageOwnerRequest>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.Property(e => e.RequestDate).HasColumnType("datetime");

            entity.HasOne(d => d.NewOwner).WithMany()
                .HasForeignKey(d => d.NewOwnerKey)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.PackageRegistration).WithMany()
                .HasForeignKey(d => d.PackageRegistrationKey)
                .HasConstraintName("FK_dbo.PackageOwnerRequests_dbo.PackageRegistrations_PackageRegistrationKey");

            entity.HasOne(d => d.RequestingOwner).WithMany()
                .HasForeignKey(d => d.RequestingOwnerKey)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<PackageRegistration>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.Property(e => e.Id)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasMany(d => d.Owners).WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "PackageRegistrationOwner",
                    r => r.HasOne<User>().WithMany()
                        .HasForeignKey("UserKey")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    l => l.HasOne<PackageRegistration>().WithMany()
                        .HasForeignKey("PackageRegistrationKey")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    j =>
                    {
                        j.HasKey("PackageRegistrationKey", "UserKey");
                        j.ToTable("PackageRegistrationOwners");
                        j.HasIndex(new[] { "UserKey" }, "IX_PackageRegistrationOwners_UserKey");
                    });

            entity.HasMany(d => d.RequiredSigners).WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "PackageRegistrationRequiredSigner",
                    r => r.HasOne<User>().WithMany()
                        .HasForeignKey("UserKey")
                        .HasConstraintName("FK_dbo.PackageRegistrationRequiredSigners_dbo.Users_UserKey"),
                    l => l.HasOne<PackageRegistration>().WithMany()
                        .HasForeignKey("PackageRegistrationKey")
                        .HasConstraintName("FK_dbo.PackageRegistrationRequiredSigners_dbo.PackageRegistrations_PackageRegistrationKey"),
                    j =>
                    {
                        j.HasKey("PackageRegistrationKey", "UserKey").HasName("PK_dbo.PackageRegistrationRequiredSigners");
                        j.ToTable("PackageRegistrationRequiredSigners");
                        j.HasIndex(new[] { "PackageRegistrationKey" }, "IX_PackageRegistrationKey");
                        j.HasIndex(new[] { "UserKey" }, "IX_UserKey");
                    });
        });

        modelBuilder.Entity<PackageRename>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.PackageRenames");

            entity.Property(e => e.UpdatedOn)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.FromPackageRegistration).WithMany()
                .HasForeignKey(d => d.FromPackageRegistrationKey)
                .HasConstraintName("FK_dbo.PackageRenames_dbo.PackageRegistrations_FromPackageRegistrationKey");

            entity.HasOne(d => d.ToPackageRegistration).WithMany()
                .HasForeignKey(d => d.ToPackageRegistrationKey)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_dbo.PackageRenames_dbo.PackageRegistrations_ToPackageRegistrationKey");
        });

        modelBuilder.Entity<PackageType>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.PackageTypes");

            entity.Property(e => e.Name).HasMaxLength(512);
            entity.Property(e => e.Version).HasMaxLength(128);

            entity.HasOne(d => d.Package).WithMany()
                .HasForeignKey(d => d.PackageKey)
                .HasConstraintName("FK_dbo.PackageTypes_dbo.Packages_PackageKey");
        });

        modelBuilder.Entity<PackageVulnerability>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.PackageVulnerabilities");

            entity.Property(e => e.AdvisoryUrl).IsRequired();
        });

        modelBuilder.Entity<ReservedNamespace>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.ReservedNamespaces");

            entity.Property(e => e.Value)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasMany(d => d.PackageRegistrations).WithMany(p => p.ReservedNamespaces)
                .UsingEntity<Dictionary<string, object>>(
                    "ReservedNamespaceRegistration",
                    r => r.HasOne<PackageRegistration>().WithMany()
                        .HasForeignKey("PackageRegistrationKey")
                        .HasConstraintName("FK_dbo.ReservedNamespaceRegistrations_dbo.PackageRegistrations_PackageRegistrationKey"),
                    l => l.HasOne<ReservedNamespace>().WithMany()
                        .HasForeignKey("ReservedNamespaceKey")
                        .HasConstraintName("FK_dbo.ReservedNamespaceRegistrations_dbo.ReservedNamespaces_ReservedNamespaceKey"),
                    j =>
                    {
                        j.HasKey("ReservedNamespaceKey", "PackageRegistrationKey").HasName("PK_dbo.ReservedNamespaceRegistrations");
                        j.ToTable("ReservedNamespaceRegistrations");
                        j.HasIndex(new[] { "PackageRegistrationKey" }, "IX_PackageRegistrationKey");
                        j.HasIndex(new[] { "ReservedNamespaceKey" }, "IX_ReservedNamespaceKey");
                    });

            entity.HasMany(d => d.Owners).WithMany(p => p.ReservedNamespaces)
                .UsingEntity<Dictionary<string, object>>(
                    "ReservedNamespaceOwner",
                    r => r.HasOne<User>().WithMany()
                        .HasForeignKey("UserKey")
                        .HasConstraintName("FK_dbo.ReservedNamespaceOwners_dbo.Users_UserKey"),
                    l => l.HasOne<ReservedNamespace>().WithMany()
                        .HasForeignKey("ReservedNamespaceKey")
                        .HasConstraintName("FK_dbo.ReservedNamespaceOwners_dbo.ReservedNamespaces_ReservedNamespaceKey"),
                    j =>
                    {
                        j.HasKey("ReservedNamespaceKey", "UserKey").HasName("PK_dbo.ReservedNamespaceOwners");
                        j.ToTable("ReservedNamespaceOwners");
                        j.HasIndex(new[] { "ReservedNamespaceKey" }, "IX_ReservedNamespaceKey");
                        j.HasIndex(new[] { "UserKey" }, "IX_UserKey");
                    });
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Key);
        });

        modelBuilder.Entity<Scope>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.Scopes");

            entity.Property(e => e.AllowedAction).IsRequired();

            entity.HasOne(d => d.Credential).WithMany()
                .HasForeignKey(d => d.CredentialKey)
                .HasConstraintName("FK_dbo.Scopes_dbo.Credentials_Credential_Key");

            entity.HasOne(d => d.Owner).WithMany()
                .HasForeignKey(d => d.OwnerKey)
                .HasConstraintName("FK_dbo.Scopes_dbo.Users_OwnerKey");
        });

        modelBuilder.Entity<SymbolPackage>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.SymbolPackages");

            entity.Property(e => e.Created).HasColumnType("datetime");
            entity.Property(e => e.Hash)
                .IsRequired()
                .HasMaxLength(256);
            entity.Property(e => e.HashAlgorithm).HasMaxLength(10);
            entity.Property(e => e.Published).HasColumnType("datetime");
            entity.Property(e => e.RowVersion)
                .IsRequired()
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Package).WithMany()
                .HasForeignKey(d => d.PackageKey)
                .HasConstraintName("FK_dbo.SymbolPackages_dbo.Packages_PackageKey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.Property(e => e.CreatedUtc).HasColumnType("datetime");
            entity.Property(e => e.EmailAddress).HasMaxLength(256);
            entity.Property(e => e.EmailConfirmationToken).HasMaxLength(32);
            entity.Property(e => e.LastFailedLoginUtc).HasColumnType("datetime");
            entity.Property(e => e.NotifyPackagePushed)
                .IsRequired()
                .HasDefaultValueSql("((1))");
            entity.Property(e => e.PasswordResetToken).HasMaxLength(32);
            entity.Property(e => e.PasswordResetTokenExpirationDate).HasColumnType("datetime");
            entity.Property(e => e.UnconfirmedEmailAddress).HasMaxLength(256);
            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserRole",
                    r => r.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleKey")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserKey")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    j =>
                    {
                        j.HasKey("UserKey", "RoleKey");
                        j.ToTable("UserRoles");
                    });
        });

        modelBuilder.Entity<UserCertificate>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.UserCertificates");

            entity.HasOne(d => d.Certificate).WithMany()
                .HasForeignKey(d => d.CertificateKey)
                .HasConstraintName("FK_dbo.UserCertificates_dbo.Certificates_CertificateKey");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserKey)
                .HasConstraintName("FK_dbo.UserCertificates_dbo.Users_UserKey");
        });

        modelBuilder.Entity<UserSecurityPolicy>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.UserSecurityPolicies");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(256);
            entity.Property(e => e.Subscription)
                .IsRequired()
                .HasMaxLength(256)
                .HasDefaultValueSql("('')");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserKey)
                .HasConstraintName("FK_dbo.UserSecurityPolicies_dbo.Users_UserKey");
        });

        modelBuilder.Entity<VulnerablePackageVersionRange>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_dbo.VulnerablePackageVersionRanges");

            entity.Property(e => e.FirstPatchedPackageVersion).HasMaxLength(64);
            entity.Property(e => e.PackageId)
                .IsRequired()
                .HasMaxLength(128);
            entity.Property(e => e.PackageVersionRange)
                .IsRequired()
                .HasMaxLength(132);

            entity.HasOne(d => d.Vulnerability).WithMany()
                .HasForeignKey(d => d.VulnerabilityKey)
                .HasConstraintName("FK_dbo.VulnerablePackageVersionRanges_dbo.PackageVulnerabilities_VulnerabilityKey");

            entity.HasMany(d => d.Packages).WithMany(p => p.VulnerablePackageRanges)
                .UsingEntity<Dictionary<string, object>>(
                    "VulnerablePackageVersionRangePackage",
                    r => r.HasOne<Package>().WithMany()
                        .HasForeignKey("PackageKey")
                        .HasConstraintName("FK_dbo.VulnerablePackageVersionRangePackages_dbo.Packages_Package_Key"),
                    l => l.HasOne<VulnerablePackageVersionRange>().WithMany()
                        .HasForeignKey("VulnerablePackageVersionRangeKey")
                        .HasConstraintName("FK_dbo.VulnerablePackageVersionRangePackages_dbo.VulnerablePackageVersionRanges_VulnerablePackageVersionRange_Key"),
                    j =>
                    {
                        j.HasKey("VulnerablePackageVersionRangeKey", "PackageKey").HasName("PK_dbo.VulnerablePackageVersionRangePackages");
                        j.ToTable("VulnerablePackageVersionRangePackages");
                        j.HasIndex(new[] { "PackageKey" }, "IX_Package_Key");
                        j.HasIndex(new[] { "VulnerablePackageVersionRangeKey" }, "IX_VulnerablePackageVersionRange_Key");
                        j.IndexerProperty<int>("VulnerablePackageVersionRangeKey").HasColumnName("VulnerablePackageVersionRange_Key");
                        j.IndexerProperty<int>("PackageKey").HasColumnName("Package_Key");
                    });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
