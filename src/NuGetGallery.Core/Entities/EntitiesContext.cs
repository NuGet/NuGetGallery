// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    [DbConfigurationType(typeof(EntitiesConfiguration))]
    public class EntitiesContext
        : ObjectMaterializedInterceptingDbContext, IEntitiesContext
    {
        private const string CertificatesThumbprintIndex = "IX_Certificates_Thumbprint";

        static EntitiesContext()
        {
            // Don't run migrations, ever!
            Database.SetInitializer<EntitiesContext>(null);
        }

        /// <summary>
        /// This constructor is provided mainly for purposes of running migrations from Package Manager console,
        /// or any other scenario where a connection string will be set after the EntitiesContext is created
        /// (and read only mode is don't care).
        /// </summary>
        public EntitiesContext()
            : this("Gallery.SqlServer", false) // Use the connection string in a web.config (if one is found)
        {
        }

        /// <summary>
        /// The NuGet Gallery code should usually use this constructor, in order to respect read only mode.
        /// </summary>
        public EntitiesContext(string connectionString, bool readOnly)
            : base(connectionString)
        {
            ReadOnly = readOnly;
        }

        public EntitiesContext(DbConnection connection, bool readOnly)
            : base(connection, contextOwnsConnection: true)
        {
            ReadOnly = readOnly;
        }

        public bool ReadOnly { get; private set; }
        public IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        public IDbSet<Credential> Credentials { get; set; }
        public IDbSet<Scope> Scopes { get; set; }
        public IDbSet<UserSecurityPolicy> UserSecurityPolicies { get; set; }
        public IDbSet<ReservedNamespace> ReservedNamespaces { get; set; }
        public IDbSet<Certificate> Certificates { get; set; }
        public IDbSet<UserCertificate> UserCertificates { get; set; }
        public IDbSet<SymbolPackage> SymbolPackages { get; set; }

        /// <summary>
        /// User or organization accounts.
        /// </summary>
        public IDbSet<User> Users { get; set; }
        public IDbSet<CVE> CVEs { get; set; }
        public IDbSet<CWE> CWEs { get; set; }

        IDbSet<T> IEntitiesContext.Set<T>()
        {
            return base.Set<T>();
        }

        public override async Task<int> SaveChangesAsync()
        {
            if (ReadOnly)
            {
                throw new ReadOnlyModeException("Save changes unavailable: the gallery is currently in read only mode, with limited service. Please try again later.");
            }

            return await base.SaveChangesAsync();
        }

        public void DeleteOnCommit<T>(T entity) where T : class
        {
            Set<T>().Remove(entity);
        }

        public void SetCommandTimeout(int? seconds)
        {
            ObjectContext.CommandTimeout = seconds;
        }

        public IDatabase GetDatabase()
        {
            return new DatabaseWrapper(Database);
        }

#pragma warning disable 618 // TODO: remove Package.Authors completely once production services definitely no longer need it
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Credential>()
                .HasKey(c => c.Key)
                .HasRequired(c => c.User)
                    .WithMany(u => u.Credentials)
                    .HasForeignKey(c => c.UserKey);

            modelBuilder.Entity<Scope>()
                .HasKey(c => c.Key);

            modelBuilder.Entity<Scope>()
                .HasOptional(sc => sc.Owner)
                .WithMany()
                .HasForeignKey(sc => sc.OwnerKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Scope>()
                .HasRequired<Credential>(sc => sc.Credential)
                .WithMany(cr => cr.Scopes)
                .HasForeignKey(sc => sc.CredentialKey)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<PackageLicenseReport>()
                .HasKey(r => r.Key)
                .HasMany(r => r.Licenses)
                .WithMany(l => l.Reports)
                .Map(c => c.ToTable("PackageLicenseReportLicenses")
                           .MapLeftKey("ReportKey")
                           .MapRightKey("LicenseKey"));

            modelBuilder.Entity<PackageLicense>()
                .HasKey(l => l.Key);

            modelBuilder.Entity<User>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<User>()
                .HasMany<EmailMessage>(u => u.Messages)
                .WithRequired(em => em.ToUser)
                .HasForeignKey(em => em.ToUserKey);

            modelBuilder.Entity<User>()
                .HasMany<Role>(u => u.Roles)
                .WithMany(r => r.Users)
                .Map(c => c.ToTable("UserRoles")
                           .MapLeftKey("UserKey")
                           .MapRightKey("RoleKey"));

            modelBuilder.Entity<Organization>()
                .ToTable("Organizations");

            modelBuilder.Entity<Membership>()
                .HasKey(m => new { m.OrganizationKey, m.MemberKey });

            modelBuilder.Entity<MembershipRequest>()
                .HasKey(m => new { m.OrganizationKey, m.NewMemberKey });

            modelBuilder.Entity<OrganizationMigrationRequest>()
                .HasKey(m => m.NewOrganizationKey);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Organizations)
                .WithRequired(m => m.Member)
                .HasForeignKey(m => m.MemberKey)
                .WillCascadeOnDelete(true); // Membership will be deleted with the Member account.

            modelBuilder.Entity<User>()
                .HasMany(u => u.OrganizationRequests)
                .WithRequired(m => m.NewMember)
                .HasForeignKey(m => m.NewMemberKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<User>()
                .HasOptional(u => u.OrganizationMigrationRequest)
                .WithRequired(m => m.NewOrganization);

            modelBuilder.Entity<User>()
                .HasMany(u => u.OrganizationMigrationRequests)
                .WithRequired(m => m.AdminUser)
                .HasForeignKey(m => m.AdminUserKey)
                .WillCascadeOnDelete(true); // Migration request will be deleted with the Admin account.

            modelBuilder.Entity<Organization>()
                .HasMany(o => o.Members)
                .WithRequired(m => m.Organization)
                .HasForeignKey(m => m.OrganizationKey)
                .WillCascadeOnDelete(true); // Memberships will be deleted with the Organization account.

            modelBuilder.Entity<Organization>()
                .HasMany(o => o.MemberRequests)
                .WithRequired(m => m.Organization)
                .HasForeignKey(m => m.OrganizationKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Role>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<UserSecurityPolicy>()
                .HasRequired<User>(p => p.User)
                .WithMany(cr => cr.SecurityPolicies)
                .HasForeignKey(p => p.UserKey)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ReservedNamespace>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<ReservedNamespace>()
                .HasMany<PackageRegistration>(rn => rn.PackageRegistrations)
                .WithMany(pr => pr.ReservedNamespaces)
                .Map(prrn => prrn.ToTable("ReservedNamespaceRegistrations")
                                .MapLeftKey("ReservedNamespaceKey")
                                .MapRightKey("PackageRegistrationKey"));

            modelBuilder.Entity<ReservedNamespace>()
                .HasMany<User>(pr => pr.Owners)
                .WithMany(u => u.ReservedNamespaces)
                .Map(c => c.ToTable("ReservedNamespaceOwners")
                           .MapLeftKey("ReservedNamespaceKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<UserSecurityPolicy>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasKey(em => em.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasOptional<User>(em => em.FromUser)
                .WithMany()
                .HasForeignKey(em => em.FromUserKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasKey(pr => pr.Key);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<Package>(pr => pr.Packages)
                .WithRequired(p => p.PackageRegistration)
                .HasForeignKey(p => p.PackageRegistrationKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<User>(pr => pr.Owners)
                .WithMany()
                .Map(c => c.ToTable("PackageRegistrationOwners")
                           .MapLeftKey("PackageRegistrationKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<PackageRegistration>()
                .HasMany(pr => pr.RequiredSigners)
                .WithMany()
                .Map(c => c.ToTable("PackageRegistrationRequiredSigners")
                           .MapLeftKey("PackageRegistrationKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<Package>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<Package>()
                .HasMany<PackageAuthor>(p => p.Authors)
                .WithRequired(pa => pa.Package)
                .HasForeignKey(pa => pa.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageDependency>(p => p.Dependencies)
                .WithRequired(pd => pd.Package)
                .HasForeignKey(pd => pd.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageType>(p => p.PackageTypes)
                .WithRequired(pt => pt.Package)
                .HasForeignKey(pt => pt.PackageKey);

            modelBuilder.Entity<Package>()
                .HasOptional(p => p.Certificate);

            modelBuilder.Entity<PackageHistory>()
                .HasKey(pm => pm.Key);

            modelBuilder.Entity<PackageHistory>()
                .HasOptional(pm => pm.User)
                .WithMany()
                .HasForeignKey(pm => pm.UserKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PackageHistory>()
                .HasRequired<Package>(pm => pm.Package)
                .WithMany(p => p.PackageHistories)
                .HasForeignKey(pm => pm.PackageKey)
                .WillCascadeOnDelete(true); // PackageHistories get deleted with their package.

            modelBuilder.Entity<PackageAuthor>()
                .HasKey(pa => pa.Key);

            modelBuilder.Entity<PackageDependency>()
                .HasKey(pd => pd.Key);

            modelBuilder.Entity<GallerySetting>()
                .HasKey(gs => gs.Key);

            modelBuilder.Entity<PackageOwnerRequest>()
                .HasKey(por => por.Key);

            modelBuilder.Entity<PackageFramework>()
                .HasKey(pf => pf.Key);

            modelBuilder.Entity<PackageDelete>()
                .HasKey(pd => pd.Key)
                .HasMany(pd => pd.Packages)
                    .WithOptional();

            modelBuilder.Entity<AccountDelete>()
                .HasKey(a => a.Key)
                .HasRequired(a => a.DeletedAccount);

            modelBuilder.Entity<AccountDelete>()
                .HasRequired(a => a.DeletedBy)
                .WithMany()
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Certificate>()
                .HasKey(c => c.Key);

            modelBuilder.Entity<Certificate>()
                .Property(c => c.Thumbprint)
                .HasMaxLength(256)
                .HasColumnType("varchar")
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
                .Property(c => c.Sha1Thumbprint)
                .HasMaxLength(40)
                .HasColumnType("varchar")
                .IsRequired();

            modelBuilder.Entity<UserCertificate>()
                .HasKey(uc => uc.Key);

            modelBuilder.Entity<User>()
                .HasMany(u => u.UserCertificates)
                .WithRequired(uc => uc.User)
                .HasForeignKey(uc => uc.UserKey)
                .WillCascadeOnDelete(true); // Deleting a User entity will also delete related UserCertificate entities.

            modelBuilder.Entity<Certificate>()
                .HasMany(c => c.UserCertificates)
                .WithRequired(uc => uc.Certificate)
                .HasForeignKey(uc => uc.CertificateKey)
                .WillCascadeOnDelete(true); // Deleting a Certificate entity will also delete related UserCertificate entities.

            modelBuilder.Entity<Certificate>()
                .Property(pv => pv.Expiration)
                .IsOptional()
                .HasColumnType("datetime2");

            modelBuilder.Entity<SymbolPackage>()
                .HasKey(s => s.Key);

            modelBuilder.Entity<Package>()
                .HasMany(p => p.SymbolPackages)
                .WithRequired(s => s.Package)
                .HasForeignKey(p => p.PackageKey);

            modelBuilder.Entity<SymbolPackage>()
                .Property(s => s.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<PackageDeprecation>()
                .HasKey(d => d.Key);

            modelBuilder.Entity<CVE>()
                .HasKey(d => d.Key)
                .Property(e => e.CVEId)
                .HasColumnType("varchar")
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<CVE>()
                .Property(e => e.Description)
                .HasMaxLength(4000)
                .IsRequired();

            modelBuilder.Entity<CWE>()
                .HasKey(d => d.Key)
                .Property(e => e.CWEId)
                .HasColumnType("varchar")
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<CWE>()
                .Property(e => e.Name)
                .HasMaxLength(200)
                .IsRequired();

            modelBuilder.Entity<CWE>()
                .Property(e => e.Description)
                .HasMaxLength(600)
                .IsRequired();

            modelBuilder.Entity<Package>()
                .HasMany(p => p.Deprecations)
                .WithRequired(d => d.Package)
                .HasForeignKey(d => d.PackageKey)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Package>()
                .HasMany(p => p.AlternativeOf)
                .WithOptional(d => d.AlternatePackage)
                .HasForeignKey(d => d.AlternatePackageKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany(p => p.AlternativeOf)
                .WithOptional(d => d.AlternatePackageRegistration)
                .HasForeignKey(d => d.AlternatePackageRegistrationKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PackageDeprecation>()
                .HasOptional(d => d.DeprecatedByUser)
                .WithMany()
                .HasForeignKey(d => d.DeprecatedByUserKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PackageDeprecation>()
                .Property(v => v.CVSSRating)
                .HasPrecision(3, 1);

            modelBuilder.Entity<PackageDeprecation>()
                .HasMany(p => p.CVEs)
                .WithMany(c => c.PackageDeprecations);

            modelBuilder.Entity<PackageDeprecation>()
                .HasMany(p => p.CWEs)
                .WithMany(c => c.PackageDeprecations);
        }
#pragma warning restore 618
    }
}
