// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Auditing.Obfuscation;

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedPackage
    {
        public int PackageRegistrationKey { get; set; }
        public string Copyright { get; set; }
        public DateTime Created { get; set; }
        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public int DownloadCount { get; set; }
        public string ExternalPackageUrl { get; set; }
        public string HashAlgorithm { get; set; }
        public string Hash { get; set; }
        public string IconUrl { get; set; }
        public bool IsLatest { get; set; }
        public bool IsLatestStable { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime? LastEdited { get; set; }
        public string LicenseUrl { get; set; }
        public bool HideLicenseReport { get; set; }
        public string Language { get; set; }
        public DateTime Published { get; set; }
        public long PackageFileSize { get; set; }
        public string ProjectUrl { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string NormalizedVersion { get; set; }
        public string LicenseNames { get; set; }
        public string LicenseReportUrl { get; set; }
        public bool Listed { get; set; }
        public bool IsPrerelease { get; set; }
        [Obfuscate(ObfuscationType.Authors)]
        public string FlattenedAuthors { get; set; }
        public string FlattenedDependencies { get; set; }
        public int Key { get; set; }
        public string MinClientVersion { get; set; }
        [Obfuscate(ObfuscationType.UserKey)]
        public int? UserKey { get; set; }
        public bool Deleted { get; set; }
        public bool HasReadMe { get; set; }
        public int PackageStatusKey { get; set; }

        public static AuditedPackage CreateFrom(Package package)
        {
            return new AuditedPackage
            {
                PackageRegistrationKey = package.PackageRegistrationKey,
                Copyright = package.Copyright,
                Created = package.Created,
                Description = package.Description,
                ReleaseNotes = package.ReleaseNotes,
                DownloadCount = package.DownloadCount,
                HashAlgorithm = package.HashAlgorithm,
                Hash = package.Hash,
                IconUrl = package.IconUrl,
                IsLatest = package.IsLatest,
                IsLatestStable = package.IsLatestStable,
                LastUpdated = package.LastUpdated,
                LastEdited = package.LastEdited,
                LicenseUrl = package.LicenseUrl,
                HideLicenseReport = package.HideLicenseReport,
                Language = package.Language,
                Published = package.Published,
                PackageFileSize = package.PackageFileSize,
                ProjectUrl = package.ProjectUrl,
                RequiresLicenseAcceptance = package.RequiresLicenseAcceptance,
                Summary = package.Summary,
                Tags = package.Tags,
                Title = package.Title,
                Version = package.Version,
                NormalizedVersion = package.NormalizedVersion,
                LicenseNames = package.LicenseNames,
                LicenseReportUrl = package.LicenseReportUrl,
                Listed = package.Listed,
                IsPrerelease = package.IsPrerelease,
                FlattenedAuthors = package.FlattenedAuthors,
                FlattenedDependencies = package.FlattenedDependencies,
                Key = package.Key,
                MinClientVersion = package.MinClientVersion,
                UserKey = package.UserKey,
#pragma warning disable CS0612 // Type or member is obsolete
                Deleted = package.Deleted,
#pragma warning restore CS0612 // Type or member is obsolete
                HasReadMe = package.HasReadMe,
                PackageStatusKey = (int)package.PackageStatusKey,
            };
        }
    }
}