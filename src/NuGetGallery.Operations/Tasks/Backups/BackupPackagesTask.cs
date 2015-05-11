// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Tasks;

namespace NuGetGallery.Operations
{
    [Command("backuppackages", "Back up all packages at the source storage server", AltName = "bps", MaxArgs = 0)]
    public class BackupPackagesTask : DatabaseAndStorageTask
    {
        [Option("The destination storage account for the backups", AltName="d")]
        public CloudStorageAccount BackupStorage { get; set; }

        [Option("Perform back ups in a single thread", AltName="s")]
        public bool SingleThreaded { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (BackupStorage == null)
            {
                BackupStorage = StorageAccount;
                if (CurrentEnvironment != null)
                {
                    BackupStorage = CurrentEnvironment.BackupStorage;
                }
            }
        }

        public override void ExecuteCommand()
        {
            Log.Info(
                    "Backing up '{0}/packages' -> '{1}/package-backups'.",
                    StorageAccount.Credentials.AccountName,
                    BackupStorage.Credentials.AccountName);

            var client = CreateBlobClient();
            var backupClient = BackupStorage.CreateCloudBlobClient();

            // Get the state file object
            var state = GetStateFile(backupClient);
            var lastId = state.LastBackedUpId;
            bool forcedRecheck = false;
            if (state.LastBackupCompletedUtc.HasValue && ((DateTimeOffset.UtcNow - state.LastBackupCompletedUtc.Value) > TimeSpan.FromDays(1)))
            {
                // Do a "full" backup (check every package file) every day
                lastId = null;
                forcedRecheck = true;
            }
            
            var packagesToBackUp = GetPackagesToBackUp(lastId, forcedRecheck);
            
            var processedCount = 0;
            
            var backupBlobs = backupClient.GetContainerReference("package-backups");
            var packageBlobs = client.GetContainerReference("packages");
            if (!WhatIf)
            {
                backupBlobs.CreateIfNotExists();
            }
            Parallel.ForEach(packagesToBackUp, new ParallelOptions { MaxDegreeOfParallelism = SingleThreaded ? 1 : 10 }, package =>
            {
                try
                {
                    var packageBlob = packageBlobs.GetBlockBlobReference(Util.GetPackageFileName(package.Id, package.Version));
                    var backupBlob = backupBlobs.GetBlockBlobReference(Util.GetPackageBackupFileName(package.Id, package.Version, package.Hash));
                    if (packageBlob.Exists())
                    {
                        bool shouldCopy = backupBlob.Exists();

                        // Verify the package, if it exists
                        if (shouldCopy)
                        {
                            packageBlob.FetchAttributes();
                            backupBlob.FetchAttributes();
                            shouldCopy = 
                                String.IsNullOrEmpty(packageBlob.Properties.ContentMD5) ||
                                String.IsNullOrEmpty(backupBlob.Properties.ContentMD5) ||
                                !String.Equals(packageBlob.Properties.ContentMD5, backupBlob.Properties.ContentMD5, StringComparison.Ordinal);
                        }

                        if (!shouldCopy && !WhatIf)
                        {
                            backupBlob.StartCopyFromBlob(packageBlob);
                        }

                        Interlocked.Increment(ref processedCount);
                        Log.Trace(
                            "[{2:000000}/{3:000000} {4:00.0}%] {5} Backup of '{0}@{1}'.",
                            package.Id,
                            package.Version,
                            processedCount,
                            packagesToBackUp.Count,
                            (double)processedCount / (double)packagesToBackUp.Count,
                            shouldCopy ? "Skipped" : "Started");
                    }
                    else
                    {
                        Log.Warn(
                            "[{2:000000}/{3:000000} {4:00.0}%] Package File not found in source: '{0}@{1}'",
                            package.Id,
                            package.Version,
                            processedCount,
                            packagesToBackUp.Count,
                            (double)processedCount / (double)packagesToBackUp.Count);
                    }

                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref processedCount);
                    Log.Error(
                        "[{2:000000}/{3:000000} {4:00.0}%] Error Starting Backup of '{0}@{1}': {5}",
                        package.Id,
                        package.Version,
                        processedCount,
                        packagesToBackUp.Count,
                        (double)processedCount / (double)packagesToBackUp.Count,
                        ex.Message);
                }
            });

            Log.Info("Backed up {0} packages from {1} to {2}", processedCount, StorageAccount.Credentials.AccountName, BackupStorage.Credentials.AccountName);

            state.LastBackupCompletedUtc = DateTimeOffset.UtcNow;
            state.LastBackedUpId = packagesToBackUp.Max(p => p.Key);

            WriteStateFile(backupClient, state);
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification="StreamReader will leave the stream open.")]
        private State GetStateFile(CloudBlobClient backupClient)
        {
            var container = backupClient.GetContainerReference("package-backups");
            container.CreateIfNotExists();
            var blob = container.GetBlockBlobReference("__backupstate.json");
            if (blob.Exists())
            {
                using (var strm = new MemoryStream())
                {
                    blob.DownloadToStream(strm);
                    strm.Flush();
                    strm.Seek(0, SeekOrigin.Begin);
                    using (var rdr = new StreamReader(strm, Encoding.Default, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                    {
                        try
                        {
                            return JsonConvert.DeserializeObject<State>(rdr.ReadToEnd());
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorException(String.Format("Error parsing state file: {0}", ex.Message), ex);
                            return new State(); // Return an empty state and continue
                        }
                    }
                }
            }
            else
            {
                return new State();
            }
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "StreamReader will leave the stream open.")]
        private static void WriteStateFile(CloudBlobClient backupClient, State state)
        {
            var container = backupClient.GetContainerReference("package-backups");
            container.CreateIfNotExists();
            var blob = container.GetBlockBlobReference("__backupstate.json");
            using (var strm = new MemoryStream())
            {
                using (var writer = new StreamWriter(strm, Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                {
                    writer.Write(JsonConvert.SerializeObject(state));
                    writer.Flush();
                }
                strm.Flush();
                strm.Seek(0, SeekOrigin.Begin);
                blob.UploadFromStream(strm);
            }
        }

        IList<Package> GetPackagesToBackUp(long? lastBackupId, bool forcedRecheck)
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                Log.Info("Getting {1} packages to back up (since Package #{0})...", lastBackupId.HasValue ? lastBackupId.Value.ToString() : "?", forcedRecheck ? "all" : "1000");

                StringBuilder uglySqlInjectionyStringBuilder = new StringBuilder(); // We trust our own code so it's not so SQL Injectiony...
                uglySqlInjectionyStringBuilder.Append("SELECT ");
                if (!forcedRecheck)
                {
                    // Back up in 1000 package chunks
                    uglySqlInjectionyStringBuilder.Append("TOP 1000 ");
                }
                uglySqlInjectionyStringBuilder.Append("p.[Key], pr.Id, p.Version, p.Hash ");
                uglySqlInjectionyStringBuilder.Append("FROM Packages p ");
                uglySqlInjectionyStringBuilder.Append("JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey ");
                uglySqlInjectionyStringBuilder.Append("WHERE p.ExternalPackageUrl IS NULL ");
                if (lastBackupId != null)
                {
                    uglySqlInjectionyStringBuilder.Append("AND p.[Key] > " + lastBackupId.Value + " ");
                }
                uglySqlInjectionyStringBuilder.Append("ORDER BY Id, Version, Hash");
                var list = dbExecutor.Query<Package>(uglySqlInjectionyStringBuilder.ToString()).ToList();
                Log.Info("Got {0} packages.", list.Count);
                return list;
            }
        }

        private class State
        {
            public long? LastBackedUpId { get; set; }
            public DateTimeOffset? LastBackupCompletedUtc { get; set; }
        }
    }
}
