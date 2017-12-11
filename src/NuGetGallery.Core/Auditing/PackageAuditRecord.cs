// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecord : AuditRecord<AuditedPackageAction>
    {
        public string Id { get; }
        public string Version { get; }
        public string Hash { get; }

        public AuditedPackage PackageRecord { get; }
        public AuditedPackageRegistration RegistrationRecord { get; }

        public string Reason { get; }

        public PackageAuditRecord(
            string id, string version, string hash,
            AuditedPackage packageRecord, AuditedPackageRegistration registrationRecord,
            AuditedPackageAction action, string reason)
            : base(action)
        {
            Id = id;
            Version = version;
            Hash = hash;
            PackageRecord = packageRecord;
            RegistrationRecord = registrationRecord;
            Reason = reason;
        }

        public PackageAuditRecord(string id, string version, AuditedPackageAction action, string reason)
            : this(id,
                  version,
                  hash: "",
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: reason)
        { }

        public PackageAuditRecord(Package package, AuditedPackageAction action, string reason)
            : this(package.PackageRegistration.Id,
                  package.Version,
                  package.Hash,
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: reason)
        {
            PackageRecord = AuditedPackage.CreateFrom(package);
            RegistrationRecord = AuditedPackageRegistration.CreateFrom(package.PackageRegistration);
        }

        public PackageAuditRecord(Package package, AuditedPackageAction action)
            : this(package.PackageRegistration.Id,
                  package.Version,
                  package.Hash,
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: null)
        {
            PackageRecord = AuditedPackage.CreateFrom(package);
            RegistrationRecord = AuditedPackageRegistration.CreateFrom(package.PackageRegistration);
        }

        public override string GetPath()
        {
            return $"{Id}/{NuGetVersionFormatter.Normalize(Version)}"
                .ToLowerInvariant();
        }

        public override AuditRecord Obfuscate()
        {
            var obfuscatedPackage = CreateObfuscatedPackage();
            var obfuscatedAuditedPackage = AuditedPackage.CreateFrom(obfuscatedPackage);

            return new PackageAuditRecord(Id, Version, Hash, obfuscatedAuditedPackage, RegistrationRecord, Action, Reason );
        }

        private Package CreateObfuscatedPackage()
        {
            Package obfuscatedPacakge = new Package();
            obfuscatedPacakge.PackageRegistrationKey = PackageRecord.PackageRegistrationKey;
            obfuscatedPacakge.Copyright = PackageRecord.Copyright;
            obfuscatedPacakge.Created = PackageRecord.Created;
            obfuscatedPacakge.Description = PackageRecord.Description;
            obfuscatedPacakge.ReleaseNotes = PackageRecord.ReleaseNotes;
            obfuscatedPacakge.DownloadCount = PackageRecord.DownloadCount;
            obfuscatedPacakge.HashAlgorithm = PackageRecord.HashAlgorithm;
            obfuscatedPacakge.Hash = PackageRecord.Hash;
            obfuscatedPacakge.IconUrl = PackageRecord.IconUrl;
            obfuscatedPacakge.IsLatest = PackageRecord.IsLatest;
            obfuscatedPacakge.IsLatestStable = PackageRecord.IsLatestStable;
            obfuscatedPacakge.LastUpdated = PackageRecord.LastUpdated;
            obfuscatedPacakge.LastEdited = PackageRecord.LastEdited;
            obfuscatedPacakge.LicenseUrl = PackageRecord.LicenseUrl;
            obfuscatedPacakge.HideLicenseReport = PackageRecord.HideLicenseReport;
            obfuscatedPacakge.Language = PackageRecord.Language;
            obfuscatedPacakge.Published = PackageRecord.Published;
            obfuscatedPacakge.PackageFileSize = PackageRecord.PackageFileSize;
            obfuscatedPacakge.ProjectUrl = PackageRecord.ProjectUrl;
            obfuscatedPacakge.RequiresLicenseAcceptance = PackageRecord.RequiresLicenseAcceptance;
            obfuscatedPacakge.Summary = PackageRecord.Summary;
            obfuscatedPacakge.Tags = PackageRecord.Tags;
            obfuscatedPacakge.Title = PackageRecord.Title;
            obfuscatedPacakge.Version = PackageRecord.Version;
            obfuscatedPacakge.NormalizedVersion = PackageRecord.NormalizedVersion;
            obfuscatedPacakge.LicenseNames = PackageRecord.LicenseNames;
            obfuscatedPacakge.LicenseReportUrl = PackageRecord.LicenseReportUrl;
            obfuscatedPacakge.Listed = PackageRecord.Listed;
            obfuscatedPacakge.IsPrerelease = PackageRecord.IsPrerelease;
            // Obfuscate FlattenedAuthors
            obfuscatedPacakge.FlattenedAuthors = string.Empty;
            obfuscatedPacakge.FlattenedDependencies = PackageRecord.FlattenedDependencies;
            obfuscatedPacakge.Key = PackageRecord.Key;
            obfuscatedPacakge.MinClientVersion = PackageRecord.MinClientVersion;
            // Obfuscate User Key
            obfuscatedPacakge.UserKey = -1;
#pragma warning disable CS0612 // Type or member is obsolete
            obfuscatedPacakge.Deleted = PackageRecord.Deleted;
#pragma warning restore CS0612 // Type or member is obsolete
            obfuscatedPacakge.HasReadMe = PackageRecord.HasReadMe;
            obfuscatedPacakge.PackageStatusKey = (PackageStatus)PackageRecord.PackageStatusKey;

            return obfuscatedPacakge;

        }
    }
}