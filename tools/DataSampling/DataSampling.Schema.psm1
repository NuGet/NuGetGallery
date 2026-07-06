<#
.SYNOPSIS
    Static description of the NuGet Gallery entity graph used by the
    data-sampling import/export scripts.

.DESCRIPTION
    Get-DsSchemaModel returns the ordered list of tables to insert on import,
    together with:
      * IdentityKey  - the identity primary-key column whose old value is mapped
                       to a freshly generated value (or $null for tables with no
                       identity, e.g. join tables and the Organizations subtype).
      * KeyMap       - the name of the old->new key map this table contributes to
                       (usually equal to the table name; $null when the table does
                       not own an identity that other tables reference).
      * Fks          - hashtable of (column -> key-map-name) used to remap foreign
                       keys to their newly generated parent keys.
      * NaturalKey   - optional single column used for an explicit fail-fast
                       pre-existence check on import.

    Table & column names were verified against src\NuGetGallery\Migrations and
    src\NuGetGallery.Core\Entities\EntitiesContext.cs (OnModelCreating).

    The order is FK-safe: parents precede children; join tables come last.
#>

Set-StrictMode -Version Latest

function Get-DsSchemaModel {
    return @(
        # --- Independent / parent tables -----------------------------------
        @{ Table = 'Roles';                  IdentityKey = 'Key'; KeyMap = 'Roles';                  Fks = @{};                                                                          NaturalKey = 'Name' }
        @{ Table = 'Certificates';           IdentityKey = 'Key'; KeyMap = 'Certificates';           Fks = @{};                                                                          NaturalKey = 'Thumbprint' }
        @{ Table = 'PackageVulnerabilities'; IdentityKey = 'Key'; KeyMap = 'PackageVulnerabilities'; Fks = @{};                                                                          NaturalKey = 'GitHubDatabaseKey' }
        @{ Table = 'Users';                  IdentityKey = 'Key'; KeyMap = 'Users';                  Fks = @{};                                                                          NaturalKey = 'Username' }

        # Organizations is the TPT subtype of Users; its PK 'Key' is a FK to Users.Key.
        @{ Table = 'Organizations';          IdentityKey = $null; KeyMap = $null;                    Fks = @{ 'Key' = 'Users' };                                                         NaturalKey = $null }

        @{ Table = 'ReservedNamespaces';     IdentityKey = 'Key'; KeyMap = 'ReservedNamespaces';     Fks = @{};                                                                          NaturalKey = 'Value' }
        @{ Table = 'PackageRegistrations';   IdentityKey = 'Key'; KeyMap = 'PackageRegistrations';   Fks = @{};                                                                          NaturalKey = 'Id' }

        # --- Packages and per-package children ------------------------------
        @{ Table = 'Packages';               IdentityKey = 'Key'; KeyMap = 'Packages';               Fks = @{ 'PackageRegistrationKey' = 'PackageRegistrations'; 'UserKey' = 'Users'; 'CertificateKey' = 'Certificates' }; NaturalKey = $null }
        @{ Table = 'VulnerablePackageVersionRanges'; IdentityKey = 'Key'; KeyMap = 'VulnerablePackageVersionRanges'; Fks = @{ 'VulnerabilityKey' = 'PackageVulnerabilities' };          NaturalKey = $null }
        @{ Table = 'PackageDependencies';    IdentityKey = 'Key'; KeyMap = 'PackageDependencies';    Fks = @{ 'PackageKey' = 'Packages' };                                               NaturalKey = $null }
        @{ Table = 'PackageFrameworks';      IdentityKey = 'Key'; KeyMap = 'PackageFrameworks';      Fks = @{ 'PackageKey' = 'Packages' };                                               NaturalKey = $null }
        @{ Table = 'PackageTypes';           IdentityKey = 'Key'; KeyMap = 'PackageTypes';           Fks = @{ 'PackageKey' = 'Packages' };                                               NaturalKey = $null }
        @{ Table = 'PackageAuthors';         IdentityKey = 'Key'; KeyMap = 'PackageAuthors';         Fks = @{ 'PackageKey' = 'Packages' };                                               NaturalKey = $null }
        @{ Table = 'PackageHistories';       IdentityKey = 'Key'; KeyMap = 'PackageHistories';       Fks = @{ 'PackageKey' = 'Packages'; 'UserKey' = 'Users' };                          NaturalKey = $null }
        @{ Table = 'SymbolPackages';         IdentityKey = 'Key'; KeyMap = 'SymbolPackages';         Fks = @{ 'PackageKey' = 'Packages' };                                               NaturalKey = $null }
        @{ Table = 'PackageDeprecations';    IdentityKey = 'Key'; KeyMap = 'PackageDeprecations';    Fks = @{ 'PackageKey' = 'Packages'; 'AlternatePackageKey' = 'Packages'; 'AlternatePackageRegistrationKey' = 'PackageRegistrations'; 'DeprecatedByUserKey' = 'Users' }; NaturalKey = $null }
        @{ Table = 'PackageRenames';         IdentityKey = 'Key'; KeyMap = 'PackageRenames';         Fks = @{ 'FromPackageRegistrationKey' = 'PackageRegistrations'; 'ToPackageRegistrationKey' = 'PackageRegistrations' }; NaturalKey = $null }

        # --- Per-user children ----------------------------------------------
        @{ Table = 'UserSecurityPolicies';   IdentityKey = 'Key'; KeyMap = 'UserSecurityPolicies';   Fks = @{ 'UserKey' = 'Users' };                                                     NaturalKey = $null }
        @{ Table = 'UserCertificates';       IdentityKey = 'Key'; KeyMap = 'UserCertificates';       Fks = @{ 'UserKey' = 'Users'; 'CertificateKey' = 'Certificates' };                  NaturalKey = $null }

        # --- Join / link tables (no identity) -------------------------------
        @{ Table = 'Memberships';                       IdentityKey = $null; KeyMap = $null; Fks = @{ 'OrganizationKey' = 'Users'; 'MemberKey' = 'Users' };                              NaturalKey = $null }
        @{ Table = 'PackageRegistrationOwners';         IdentityKey = $null; KeyMap = $null; Fks = @{ 'PackageRegistrationKey' = 'PackageRegistrations'; 'UserKey' = 'Users' };          NaturalKey = $null }
        @{ Table = 'PackageRegistrationRequiredSigners';IdentityKey = $null; KeyMap = $null; Fks = @{ 'PackageRegistrationKey' = 'PackageRegistrations'; 'UserKey' = 'Users' };          NaturalKey = $null }
        @{ Table = 'UserRoles';                         IdentityKey = $null; KeyMap = $null; Fks = @{ 'UserKey' = 'Users'; 'RoleKey' = 'Roles' };                                        NaturalKey = $null }
        @{ Table = 'ReservedNamespaceOwners';           IdentityKey = $null; KeyMap = $null; Fks = @{ 'ReservedNamespaceKey' = 'ReservedNamespaces'; 'UserKey' = 'Users' };             NaturalKey = $null }
        @{ Table = 'ReservedNamespaceRegistrations';    IdentityKey = $null; KeyMap = $null; Fks = @{ 'ReservedNamespaceKey' = 'ReservedNamespaces'; 'PackageRegistrationKey' = 'PackageRegistrations' }; NaturalKey = $null }
        @{ Table = 'VulnerablePackageVersionRangePackages'; IdentityKey = $null; KeyMap = $null; Fks = @{ 'VulnerablePackageVersionRange_Key' = 'VulnerablePackageVersionRanges'; 'Package_Key' = 'Packages' }; NaturalKey = $null }
    )
}

Export-ModuleMember -Function Get-DsSchemaModel
