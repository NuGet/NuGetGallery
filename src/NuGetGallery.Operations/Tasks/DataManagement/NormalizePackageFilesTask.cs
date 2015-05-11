// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations.Tasks.DataManagement
{
    [Command("normalizepackagefiles", "Copies package files to a file name based on their normalized package version", AltName = "npf")]
    public class NormalizePackageFilesTask : DatabaseAndStorageTask
    {
        public override void ExecuteCommand()
        {
            WithConnection((c, db) =>
            {
                Log.Trace("Collecting list of packages...");
                var packages = db.Query<Package>(@"
                    SELECT pr.Id, p.[Key], p.Version, p.NormalizedVersion
                    FROM Packages p
                        INNER JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey
                    WHERE p.NormalizedVersion IS NOT NULL AND p.Version != p.NormalizedVersion")
                        .ToList();
                Log.Trace("Collected {0} packages to normalize", packages.Count);

                // Check for duplicates
                Log.Trace("Scanning for duplicate data...");
                var dupes = packages
                    .GroupBy(p => Tuple.Create(p.Id, p.NormalizedVersion))
                    .Where(g => g.Count() > 1)
                    .ToList();
                Log.Trace("Found {0} dupes:", dupes.Count);
                foreach (var dupe in dupes)
                {
                    Log.Debug(" * {0} {1}: {2}", dupe.Key.Item1, dupe.Key.Item2, String.Join(", ", dupe.Select(p => p.Version)));
                }
                var deduped = packages.Except(dupes.SelectMany(g => g)).ToList();
                Log.Trace("Ignoring dupes, {0} remaining", deduped.Count);

                // Copy packages
                var blobs = CreateBlobClient();
                var container = blobs.GetContainerReference("packages");
                var copyTargets = new List<CloudBlockBlob>();
                int counter = 0;
                foreach (var package in deduped)
                {
                    var blob = Util.GetPackageFileBlob(container, package.Id, package.Version);
                    if (blob.Exists())
                    {
                        var normalizedBlob = Util.GetPackageFileBlob(container, package.Id, package.NormalizedVersion);
                        if (normalizedBlob.Exists())
                        {
                            Log.Warn("Normalized Blob exists: {0}", normalizedBlob.Name);
                        }
                        else
                        {
                            if (!WhatIf)
                            {
                                normalizedBlob.StartCopyFromBlob(blob);
                                copyTargets.Add(normalizedBlob);
                            }
                            Log.Info("[{2}] {0} => {1}", blob.Name, normalizedBlob.Name, Util.GenerateStatusString(deduped.Count, ref counter));
                        }
                    }
                }
                Log.Info("Copies started. Waiting for completion");

                if (!WhatIf)
                {
                    foreach (var copyTarget in copyTargets)
                    {
                        do
                        {
                            copyTarget.FetchAttributes();
                        } while (copyTarget.CopyState.Status == CopyStatus.Pending);
                        Log.Info("{0} done.", copyTarget.Name);
                    }
                }
            });
        }
    }
}
