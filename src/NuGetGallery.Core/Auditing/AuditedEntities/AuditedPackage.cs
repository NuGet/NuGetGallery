// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGetGallery.Auditing.Obfuscation;

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedPackage
    {
        public int PackageRegistrationKey { get; private set; }
        public string Copyright { get; private set; }
        public DateTime Created { get; private set; }
        public string Description { get; private set; }
        public string ReleaseNotes { get; private set; }
        public int DownloadCount { get; private set; }
        public string ExternalPackageUrl { get; private set; }
        public string HashAlgorithm { get; private set; }
        public string Hash { get; private set; }
        public string IconUrl { get; private set; }
        public bool IsLatest { get; private set; }
        public bool IsLatestStable { get; private set; }
        public DateTime LastUpdated { get; private set; }
        public DateTime? LastEdited { get; private set; }
        public string LicenseUrl { get; private set; }
        public bool HideLicenseReport { get; private set; }
        public string Language { get; private set; }
        public DateTime Published { get; private set; }
        public long PackageFileSize { get; private set; }
        public string ProjectUrl { get; private set; }
        public bool RequiresLicenseAcceptance { get; private set; }
        public bool DevelopmentDependency { get; private set; }
        public string Summary { get; private set; }
        public string Tags { get; private set; }
        public string Title { get; private set; }
        public string Version { get; private set; }
        public string NormalizedVersion { get; private set; }
        public string LicenseNames { get; private set; }
        public string LicenseReportUrl { get; private set; }
        public bool Listed { get; private set; }
        public bool IsPrerelease { get; private set; }
        [Obfuscate(ObfuscationType.Authors)]
        public string FlattenedAuthors { get; private set; }
        public string FlattenedDependencies { get; private set; }
        public int Key { get; private set; }
        public string MinClientVersion { get; private set; }
        [Obfuscate(ObfuscationType.UserKey)]
        public int? UserKey { get; private set; }
        public bool Deleted { get; private set; }
        public bool HasReadMe { get; private set; }
        public int PackageStatusKey { get; private set; }

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
                DevelopmentDependency = package.DevelopmentDependency,
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