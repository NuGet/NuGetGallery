// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("deleteduplicatepackageversions", "Deletes Duplicate Package Versions", AltName = "ddpv", IsSpecialPurpose = true)]
    public class DeleteDuplicatePackageVersionsTask : DatabaseAndStorageTask
    {
        [Option("Storage account in which to place audit records and backups, usually provided by the environment")]
        public CloudStorageAccount BackupStorage { get; set; }

        [Option("Set this flag to write the deletion audit record ONLY and not proceed with the deletion itself")]
        public bool AuditOnly { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (BackupStorage == null && CurrentEnvironment != null)
            {
                BackupStorage = CurrentEnvironment.BackupStorage;
            }
            ArgCheck.RequiredOrConfig(BackupStorage, "BackupStorage");
        }
        public override void ExecuteCommand()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                // Query for packages
                Log.Info("Gathering list of packages...");
                var packages = dbExecutor.Query<PackageSummary>(@"
                    SELECT p.[Key], p.PackageRegistrationKey, r.Id, p.Version, p.Hash, p.LastUpdated, p.Published, p.Listed, p.IsLatestStable
                    FROM   Packages p
                    INNER JOIN PackageRegistrations r ON p.PackageRegistrationKey = r.[Key]");

                // Group by Id and and SemVer
                Log.Info("Grouping by Package ID and Actual Version...");
                var groups = packages.GroupBy(p => new { p.Id, Version = SemanticVersionExtensions.Normalize(p.Version) });

                // Find any groups with more than one entry
                Log.Info("Finding Duplicates...");
                var dups = groups.Where(g => g.Count() > 1);

                // Print them out
                int dupsUnlistedCount = 0;
                int latestCount = 0;
                foreach (var dup in dups)
                {
                    ProcessDuplicate(dup.Key.Id, dup.Key.Version, dup.ToList(), ref dupsUnlistedCount, ref latestCount);
                }
                var totalDupes = dups.Count();
                Log.Info("Found {0} Packages with duplicates.", totalDupes);
                Log.Info(" {0} of them have no listed duplicates.", dupsUnlistedCount);
                Log.Info(" {0} of them have multiple listed duplicates.", totalDupes - dupsUnlistedCount);
                if (latestCount > 0)
                {
                    Log.Warn(" {0} of them are the latest version of the relevant package", latestCount);
                }
                else
                {
                    Log.Info(" NONE of them are the latest version of the relevant package");
                }
            }
        }

        private void ProcessDuplicate(string id, string normalVersion, List<PackageSummary> packages, ref int unlistedCount, ref int latestCount)
        {
            // Are any of these the latest version?
            var latest = packages.Where(p => p.Latest).ToList();
            if (latest.Count > 0)
            {
                latestCount++;
                Log.Error("Unable to process: {0}@{1}, it is the latest version of {0}", id, normalVersion);
            }
            else
            {
                // Is there only one listed version?
                var listed = packages.Where(p => p.Listed).ToList();
                if (listed.Count == 1)
                {
                    unlistedCount++;
                    Log.Info("Cleaning {0}@{1} by removing unlisted versions", id, normalVersion);
                    foreach (var package in packages.Where(p => !p.Listed))
                    {
                        Log.Trace("Deleting {0}@{1}...", package.Id, package.Version);
                        DeletePackageVersion(package, "unlisted duplicate");
                    }
                }
                else
                {
                    // Select the most recent pacakge
                    var selected = packages.OrderByDescending(p => p.Published).FirstOrDefault();
                    if (selected == null)
                    {
                        Log.Error("Weird. There wasn't a most recent upload of {0}@{1}?", id, normalVersion);
                    }
                    else
                    {
                        Log.Info("Cleaning {0}@{1} by removing older duplicate versions", id, normalVersion);
                        foreach (var package in packages.OrderByDescending(p => p.Published).Skip(1))
                        {
                            Log.Trace("Deleting {0}@{1}...", package.Id, package.Version);
                            DeletePackageVersion(package, "older duplicate");
                        }
                    }
                }
            }
        }

        private void DeletePackageVersion(PackageSummary package, string subreason)
        {
            new DeletePackageVersionTask()
            {
                BackupStorage = BackupStorage,
                StorageAccount = StorageAccount,
                ConnectionString = ConnectionString,
                AuditOnly = AuditOnly,
                WhatIf = WhatIf,
                PackageId = package.Id,
                PackageVersion = package.Version,
                Reason = String.Format("duplicate package versions ({0})", subreason)
            }.Execute();
        }
    }

    public class PackageOwner
    {
        public string Username { get; set; }
        public string EmailAddress { get; set; }
    }

    public class PackageSummary
    {
        public int Key { get; set; }
        public int PackageRegistrationKey { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }
        public bool Listed { get; set; }
        public bool Latest { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime Published { get; set; }
    }
}
