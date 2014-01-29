using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Configuration;
using NuGet.Services.Storage;
using NuGet.Services.Work.Jobs.Models;

namespace NuGet.Services.Work.Jobs
{
    [Description("Handles pending package edits")]
    public class HandlePackageEditsJob : JobHandler<HandlePackageEditsEventSource>
    {
        private const string HashAlgorithmName = "SHA512";
        public static readonly long DefaultMaxAllowedManifestBytes = 10 /* Mb */ * 1024 /* Kb */ * 1024; /* b */

        public static readonly string GetEditsBaseSql = @"
            SELECT pr.Id, p.NormalizedVersion AS Version, p.Hash, e.*
            FROM PackageEdits e
            INNER JOIN Packages p ON p.[Key] = e.PackageKey
            INNER JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey";

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the source for package blobs
        /// </summary>
        public CloudStorageAccount Source { get; set; }
        public string SourceContainerName { get; set; }

        /// <summary>
        /// Gets or sets a connection string to the database containing package data.
        /// </summary>
        public SqlConnectionStringBuilder PackageDatabase { get; set; }

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the backup storage for package blobs
        /// </summary>
        public CloudStorageAccount Backups { get; set; }
        public string BackupsContainerName { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of tries that are allowed before considering an edit failed.
        /// </summary>
        public int? MaxTryCount { get; set; }

        public long? MaxAllowedManifestBytes { get; set; }

        protected CloudBlobContainer SourceContainer { get; set; }
        protected CloudBlobContainer BackupsContainer { get; set; }

        protected StorageHub Storage { get; set; }
        protected ConfigurationHub Config { get; set; }
        protected long MaxManifestSize { get; set; }

        public HandlePackageEditsJob(StorageHub storage, ConfigurationHub config)
        {
            Storage = storage;
            Config = config;
        }

        protected internal override async Task Execute()
        {
            // Load defaults
            MaxManifestSize = MaxAllowedManifestBytes ?? DefaultMaxAllowedManifestBytes;
            PackageDatabase = PackageDatabase ?? Config.Sql.GetConnectionString(KnownSqlServer.Legacy);
            Source = Source ?? Storage.Legacy.Account;
            Backups = Backups ?? Storage.Backup.Account;
            SourceContainer = Source.CreateCloudBlobClient().GetContainerReference(
                String.IsNullOrEmpty(SourceContainerName) ? BlobContainerNames.LegacyPackages : SourceContainerName);
            BackupsContainer = Backups.CreateCloudBlobClient().GetContainerReference(
                String.IsNullOrEmpty(BackupsContainerName) ? BlobContainerNames.Backups : BackupsContainerName);

            // Grab package edits
            IList<PackageEdit> edits;
            Log.FetchingQueuedEdits(PackageDatabase.DataSource, PackageDatabase.InitialCatalog);
            using (var connection = await PackageDatabase.ConnectTo())
            {
                if (MaxTryCount.HasValue)
                {
                    edits = (await connection.QueryAsync<PackageEdit>(
                        GetEditsBaseSql + @"
                        WHERE [TriedCount] < @MaxTryCount",
                        new
                        {
                            MaxTryCount = MaxTryCount.Value
                        })).ToList();
                }
                else
                {
                    edits = (await connection.QueryAsync<PackageEdit>(GetEditsBaseSql))
                        .ToList();
                }
            }
            Log.FetchedQueuedEdits(PackageDatabase.DataSource, PackageDatabase.InitialCatalog, edits.Count);

            // Group by package and take just the most recent edit for each package
            edits = edits
                .GroupBy(e => e.PackageKey)
                .Select(g => g.OrderByDescending(e => e.Timestamp).FirstOrDefault())
                .Where(e => e != null)
                .ToList();

            // Process packages
            foreach (var edit in edits)
            {
                Log.EditingPackage(edit.Id, edit.Version);
                Exception thrown = null;
                try
                {
                    await ApplyEdit(edit);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
                if (thrown != null)
                {
                    Log.ErrorEditingPackage(edit.Id, edit.Version, thrown.ToString());
                    if (!WhatIf)
                    {
                        using (var connection = await PackageDatabase.ConnectTo())
                        {
                            await connection.QueryAsync<int>(@"
                            UPDATE  PackageEdits
                            SET
                                    TriedCount = TriedCount + 1,
                                    LastError = @error
                            WHERE   [Key] = @key", new
                                {
                                    error = thrown.ToString(),
                                    key = edit.Key
                                });
                        }
                    }
                }
                Log.EditedPackage(edit.Id, edit.Version);
            }
        }

        private static readonly Regex ManifestSelector = new Regex(@"^[^/]*\.nuspec$", RegexOptions.IgnoreCase);
        private async Task ApplyEdit(PackageEdit edit)
        {
            // Download the original file
            string originalPath = null;
            try
            {
                string directory = Path.Combine(TempDirectory, edit.Id, edit.Version);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                originalPath = Path.Combine(directory, "original.nupkg");
                var sourceBlob = SourceContainer.GetBlockBlobReference(
                    PackageHelpers.GetPackageBlobName(edit.Id, edit.Version));
                Log.DownloadingOriginal(edit.Id, edit.Version);
                await sourceBlob.DownloadToFileAsync(originalPath, FileMode.Create);
                Log.DownloadedOriginal(edit.Id, edit.Version);

                // Check that a backup exists
                var backupBlob = BackupsContainer.GetBlockBlobReference(
                    PackageHelpers.GetPackageBackupBlobName(edit.Id, edit.Version, edit.Hash));
                if (!WhatIf && !await backupBlob.ExistsAsync())
                {
                    Log.BackingUpOriginal(edit.Id, edit.Version);
                    await backupBlob.UploadFromFileAsync(originalPath, FileMode.Open);
                    Log.BackedUpOriginal(edit.Id, edit.Version);
                }

                // Load the zip file and find the manifest
                using (var originalStream = File.Open(originalPath, FileMode.Open, FileAccess.ReadWrite))
                using (var archive = new ZipArchive(originalStream, ZipArchiveMode.Update))
                {
                    // Find the nuspec
                    var nuspecEntries = archive.Entries.Where(e => ManifestSelector.IsMatch(e.FullName)).ToArray();
                    if (nuspecEntries.Length == 0)
                    {
                        throw new InvalidDataException(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.HandlePackageEditsJob_MissingManifest,
                            edit.Id,
                            edit.Version,
                            backupBlob.Uri.AbsoluteUri));
                    }
                    else if (nuspecEntries.Length > 1)
                    {
                        throw new InvalidDataException(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.HandlePackageEditsJob_MultipleManifests,
                            edit.Id,
                            edit.Version,
                            backupBlob.Uri.AbsoluteUri));
                    }

                    // We now have the nuspec
                    var manifestEntry = nuspecEntries.Single();

                    // Load the manifest with a constrained stream
                    Log.RewritingPackage(edit.Id, edit.Version);
                    Manifest manifest;
                    using (var manifestStream = manifestEntry.Open())
                    {
                        manifest = Manifest.ReadFrom(manifestStream, validateSchema: false);
                        
                        // Modify the manifest as per the edit
                        edit.ApplyTo(manifest.Metadata);
                    
                        // Save the manifest back
                        manifestStream.Seek(0, SeekOrigin.Begin);
                        manifestStream.SetLength(0);
                        manifest.Save(manifestStream);
                    }
                    Log.RewrotePackage(edit.Id, edit.Version);
                }

                // Snapshot the original blob
                Log.SnapshottingBlob(edit.Id, edit.Version, sourceBlob.Uri.AbsoluteUri);
                var sourceSnapshot = await sourceBlob.CreateSnapshotAsync();
                Log.SnapshottedBlob(edit.Id, edit.Version, sourceBlob.Uri.AbsoluteUri, sourceSnapshot.Uri.AbsoluteUri);

                // Upload the updated file
                Log.UploadingModifiedPackage(edit.Id, edit.Version, sourceBlob.Uri.AbsoluteUri);
                await sourceBlob.UploadFromFileAsync(originalPath, FileMode.Open);
                Log.UploadedModifiedPackage(edit.Id, edit.Version, sourceBlob.Uri.AbsoluteUri);

                // Calculate new size and hash
                string hash;
                long size;
                using (var originalStream = File.OpenRead(originalPath))
                {
                    size = originalStream.Length;

                    var hashAlgorithm = HashAlgorithm.Create(HashAlgorithmName);
                    hash = Convert.ToBase64String(
                        hashAlgorithm.ComputeHash(originalStream));
                }

                // Update the database
                try
                {
                    Log.UpdatingDatabase(edit.Id, edit.Version);
                    using (var connection = await PackageDatabase.ConnectTo())
                    {
                        var parameters = new DynamicParameters(new
                        {
                            edit.Authors,
                            edit.Copyright,
                            edit.Description,
                            edit.IconUrl,
                            edit.LicenseUrl,
                            edit.ProjectUrl,
                            edit.ReleaseNotes,
                            edit.RequiresLicenseAcceptance,
                            edit.Summary,
                            edit.Title,
                            edit.Tags,
                            edit.Key,
                            edit.PackageKey,
                            edit.UserKey,
                            PackageFileSize = size,
                            Hash = hash,
                            HashAlgorithm = HashAlgorithmName
                        });

                        // Prep SQL for merging in authors
                        StringBuilder loadAuthorsSql = new StringBuilder();
                        var authors = edit.Authors.Split(',');
                        for(int i = 0; i < authors.Length; i++)
                        {
                            loadAuthorsSql.Append("INSERT INTO [PackageAuthors]([PackageKey],[Name]) VALUES(@PackageKey, @Author" + i.ToString() + ")");
                            parameters.Add("Author" + i.ToString(), authors[i]);
                        }

                        await connection.QueryAsync<int>(@"
                            BEGIN TRANSACTION
                                -- Form a comma-separated list of authors
                                DECLARE @existingAuthors nvarchar(MAX)
                                SELECT @existingAuthors = COALESCE(@existingAuthors + ',', '') + Name
                                FROM PackageAuthors
                                WHERE PackageKey = @PackageKey

                                -- Copy packages data to package history table
                                INSERT INTO [PackageHistories]
                                SELECT      [Key] AS PackageKey,
                                            @UserKey AS UserKey,
                                            GETUTCDATE() AS Timestamp,
                                            Title,
                                            @existingAuthors AS Authors,
                                            Copyright,
                                            Description,
                                            IconUrl,
                                            LicenseUrl,
                                            ProjectUrl,
                                            ReleaseNotes,
                                            RequiresLicenseAcceptance,
                                            Summary,
                                            Tags,
                                            Hash,
                                            HashAlgorithm,
                                            PackageFileSize,
                                            LastUpdated,
                                            Published
                                FROM        [Packages]
                                WHERE       [Key] = @PackageKey

                                -- Update the packages table
                                UPDATE  [Packages]
                                SET     Copyright = @Copyright,
                                        Description = @Description,
                                        IconUrl = @IconUrl,
                                        LicenseUrl = @LicenseUrl,
                                        ProjectUrl = @ProjectUrl,
                                        ReleaseNotes = @ReleaseNotes,
                                        RequiresLicenseAcceptance = @RequiresLicenseAcceptance,
                                        Summary = @Summary,
                                        Title = @Title,
                                        Tags = @Tags,
                                        LastEdited = GETUTCDATE(),
                                        LastUpdated = GETUTCDATE(),
                                        UserKey = @UserKey,
                                        Hash = @Hash,
                                        HashAlgorithm = @HashAlgorithm,
                                        PackageFileSize = @PackageFileSize,
                                        FlattenedAuthors = @Authors
                                WHERE   [Key] = @PackageKey

                                -- Update Authors
                                DELETE FROM [PackageAuthors] 
                                WHERE PackageKey = @PackageKey

                                " + loadAuthorsSql.ToString() + @"
                            
                                -- Clean this edit and all previous edits.
                                DELETE FROM [PackageEdits]
                                WHERE [PackageKey] = @PackageKey
                                AND [Key] <= @Key
                            " + (WhatIf ? "ROLLBACK TRANSACTION" : "COMMIT TRANSACTION"), 
                            parameters);
                    }
                    Log.UpdatedDatabase(edit.Id, edit.Version);
                }
                catch (Exception)
                {
                    // Error occurred while updaing database, roll back the blob to the snapshot
                    // Can't do "await" in a catch block, but this should be pretty quick since it just starts the copy
                    Log.RollingBackBlob(edit.Id, edit.Version, sourceSnapshot.Uri.AbsoluteUri, sourceBlob.Uri.AbsoluteUri);
                    sourceBlob.StartCopyFromBlob(sourceSnapshot);
                    Log.RolledBackBlob(edit.Id, edit.Version, sourceSnapshot.Uri.AbsoluteUri, sourceBlob.Uri.AbsoluteUri);
                    throw;
                }

                Log.DeletingSnapshot(edit.Id, edit.Version, sourceSnapshot.Uri.AbsoluteUri);
                await sourceSnapshot.DeleteAsync();
                Log.DeletedSnapshot(edit.Id, edit.Version, sourceSnapshot.Uri.AbsoluteUri);
            }
            finally
            {
                if (!String.IsNullOrEmpty(originalPath) && File.Exists(originalPath))
                {
                    File.Delete(originalPath);
                }
            }
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-HandlePackageEdits")]
    public class HandlePackageEditsEventSource : EventSource
    {
        public static readonly HandlePackageEditsEventSource Log = new HandlePackageEditsEventSource();
        private HandlePackageEditsEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Task = Tasks.FetchingQueuedEdits,
            Opcode = EventOpcode.Start,
            Message = "Fetching queued edits from {0}/{1}")]
        public void FetchingQueuedEdits(string server, string database) { WriteEvent(1, server, database); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Task = Tasks.FetchingQueuedEdits,
            Opcode = EventOpcode.Stop,
            Message = "Fetched {2} queued edits from {0}/{1}")]
        public void FetchedQueuedEdits(string server, string database, int edits) { WriteEvent(2, server, database, edits); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Task = Tasks.EditingPackage,
            Opcode = EventOpcode.Start,
            Message = "Editing {0} {1}")]
        public void EditingPackage(string id, string version) { WriteEvent(3, id, version); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Task = Tasks.EditingPackage,
            Opcode = EventOpcode.Stop,
            Message = "Edited {0} {1}")]
        public void EditedPackage(string id, string version) { WriteEvent(4, id, version); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = Tasks.DownloadingOriginal,
            Opcode = EventOpcode.Start,
            Message = "Downloading original copy of {0} {1}")]
        public void DownloadingOriginal(string id, string version) { WriteEvent(5, id, version); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Task = Tasks.DownloadingOriginal,
            Opcode = EventOpcode.Stop,
            Message = "Downloaded original copy of {0} {1}")]
        public void DownloadedOriginal(string id, string version) { WriteEvent(6, id, version); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = Tasks.BackingUpOriginal,
            Opcode = EventOpcode.Start,
            Message = "Backing up original copy of {0} {1}")]
        public void BackingUpOriginal(string id, string version) { WriteEvent(7, id, version); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Task = Tasks.BackingUpOriginal,
            Opcode = EventOpcode.Stop,
            Message = "Backed up original copy of {0} {1}")]
        public void BackedUpOriginal(string id, string version) { WriteEvent(8, id, version); }

        [Event(
            eventId: 9,
            Level = EventLevel.Informational,
            Task = Tasks.RewritingPackageFile,
            Opcode = EventOpcode.Start,
            Message = "Rewriting package file for {0} {1}")]
        public void RewritingPackage(string id, string version) { WriteEvent(9, id, version); }

        [Event(
            eventId: 10,
            Level = EventLevel.Informational,
            Task = Tasks.RewritingPackageFile,
            Opcode = EventOpcode.Stop,
            Message = "Rewrote package file for {0} {1}")]
        public void RewrotePackage(string id, string version) { WriteEvent(10, id, version); }

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Task = Tasks.UploadingPackageFile,
            Opcode = EventOpcode.Start,
            Message = "Uploading modified package file for {0} {1} to {2}")]
        public void UploadingModifiedPackage(string id, string version, string url) { WriteEvent(11, id, version, url); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Task = Tasks.UploadingPackageFile,
            Opcode = EventOpcode.Stop,
            Message = "Uploaded modified package file for {0} {1} to {2}")]
        public void UploadedModifiedPackage(string id, string version, string url) { WriteEvent(12, id, version, url); }

        [Event(
            eventId: 13,
            Level = EventLevel.Informational,
            Task = Tasks.UpdatingDatabase,
            Opcode = EventOpcode.Start,
            Message = "Updating package record for {0} {1}")]
        public void UpdatingDatabase(string id, string version) { WriteEvent(13, id, version); }

        [Event(
            eventId: 14,
            Level = EventLevel.Informational,
            Task = Tasks.UpdatingDatabase,
            Opcode = EventOpcode.Stop,
            Message = "Updated package record for {0} {1}")]
        public void UpdatedDatabase(string id, string version) { WriteEvent(14, id, version); }

        [Event(
            eventId: 15,
            Level = EventLevel.Informational,
            Task = Tasks.RollingBackBlob,
            Opcode = EventOpcode.Start,
            Message = "Rolling back updated blob for {0} {1}. Copying snapshot {2} to {3}")]
        public void RollingBackBlob(string id, string version, string snapshotUrl, string url) { WriteEvent(15, id, version, snapshotUrl, url); }

        [Event(
            eventId: 16,
            Level = EventLevel.Informational,
            Task = Tasks.RollingBackBlob,
            Opcode = EventOpcode.Stop,
            Message = "Rolled back updated blob for {0} {1}. Copied snapshot {2} to {3}")]
        public void RolledBackBlob(string id, string version, string snapshotUrl, string url) { WriteEvent(16, id, version, snapshotUrl, url); }

        [Event(
            eventId: 17,
            Level = EventLevel.Informational,
            Task = Tasks.SnapshottingBlob,
            Opcode = EventOpcode.Start,
            Message = "Snapshotting original blob for {0} {1} ({2}).")]
        public void SnapshottingBlob(string id, string version, string url) { WriteEvent(17, id, version, url); }

        [Event(
            eventId: 18,
            Level = EventLevel.Informational,
            Task = Tasks.SnapshottingBlob,
            Opcode = EventOpcode.Stop,
            Message = "Snapshotting original blob for {0} {1}. Made snapshot of {2} at {3}")]
        public void SnapshottedBlob(string id, string version, string url, string snapshotUrl) { WriteEvent(18, id, version, url, snapshotUrl); }

        [Event(
            eventId: 19,
            Level = EventLevel.Informational,
            Task = Tasks.DeletingSnapshot,
            Opcode = EventOpcode.Start,
            Message = "Deleting snapshot blob {2} for {0} {1}.")]
        public void DeletingSnapshot(string id, string version, string snapshotUrl) { WriteEvent(19, id, version, snapshotUrl); }

        [Event(
            eventId: 20,
            Level = EventLevel.Informational,
            Task = Tasks.DeletingSnapshot,
            Opcode = EventOpcode.Stop,
            Message = "Deleted snapshot blob {2} for {0} {1}.")]
        public void DeletedSnapshot(string id, string version, string snapshotUrl) { WriteEvent(20, id, version, snapshotUrl); }

        [Event(
            eventId: 21,
            Level = EventLevel.Error,
            Task = Tasks.EditingPackage,
            Message = "Error editing package {0} {1}: {2}")]
        public void ErrorEditingPackage(string id, string version, string error) { WriteEvent(21, id, version, error); }

        public static class Tasks
        {
            public const EventTask FetchingQueuedEdits = (EventTask)0x1;
            public const EventTask EditingPackage = (EventTask)0x2;
            public const EventTask DownloadingOriginal = (EventTask)0x3;
            public const EventTask BackingUpOriginal = (EventTask)0x4;
            public const EventTask RewritingPackageFile = (EventTask)0x5;
            public const EventTask UploadingPackageFile = (EventTask)0x6;
            public const EventTask UpdatingDatabase = (EventTask)0x7;
            public const EventTask RollingBackBlob = (EventTask)0x8;
            public const EventTask SnapshottingBlob = (EventTask)0x9;
            public const EventTask DeletingSnapshot = (EventTask)0xA;
        }
    }
}
