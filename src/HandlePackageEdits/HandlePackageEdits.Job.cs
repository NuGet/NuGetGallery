// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;
using NuGet.Services.Logging;
using NuGetGallery.Packaging;

namespace HandlePackageEdits
{
    internal class Job : JobBase
    {
        private const string HashAlgorithmName = "SHA512";
        public static readonly long DefaultMaxAllowedManifestBytes = 10 /* Mb */ * 1024 /* Kb */ * 1024; /* b */

        public static readonly string GetEditsBaseSql = @"
            SELECT pr.Id, p.NormalizedVersion AS Version, p.Hash, p.HasReadMe, e.*
            FROM PackageEdits e
            INNER JOIN Packages p ON p.[Key] = e.PackageKey
            INNER JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey";

        public const string DefaultSourceContainerName = "packages";
        public const string DefaultBackupContainerName = "package-backups";
        public const string DefaultReadMeContainerName = "readmes";
        public const int DefaultMaxRetryCount = 10;

        private const string ReadMeChanged = "changed";
        private const string ReadMeDeleted = "deleted";
        private const string MarkdownExtension = "md";
        private const string HtmlExtension = "html";

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
        /// Gets or sets an Azure Storage Uri referring to a container to use as the storage for ReadMes
        /// </summary>
        public string ReadMeContainerName { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of tries that are allowed before considering an edit failed.
        /// </summary>
        public int? MaxTryCount { get; set; }

        public long? MaxAllowedManifestBytes { get; set; }

        private CloudBlobContainer SourceContainer { get; set; }
        private CloudBlobContainer BackupsContainer { get; set; }
        private CloudBlobContainer ReadMeContainer { get; set; }
        protected long MaxManifestSize { get; set; }
        private ILogger _logger;

        private class ReadMeBlobs
        {
            public CloudBlockBlob activeBlob = null;
            public CloudBlockBlob pendingBlob = null;
            public CloudBlockBlob activeSnapshot = null;
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerFactory = LoggingSetup.CreateLoggerFactory();
                _logger = loggerFactory.CreateLogger<Job>();

                var retrievedMaxManifestSize = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.MaxManifestSize);
                MaxManifestSize = retrievedMaxManifestSize == null
                    ? DefaultMaxAllowedManifestBytes
                    : Convert.ToInt64(retrievedMaxManifestSize);

                PackageDatabase = new SqlConnectionStringBuilder(
                            JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageDatabase));

                Source = CloudStorageAccount.Parse(
                                           JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SourceStorage));
                Backups = CloudStorageAccount.Parse(
                                           JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.BackupStorage));
                SourceContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.SourceContainerName) ?? DefaultSourceContainerName;
                BackupsContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.BackupContainerName) ?? DefaultBackupContainerName;
                ReadMeContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.ReadMeContainerName) ?? DefaultReadMeContainerName;

                SourceContainer = Source.CreateCloudBlobClient().GetContainerReference(SourceContainerName);
                BackupsContainer = Backups.CreateCloudBlobClient().GetContainerReference(BackupsContainerName);
                ReadMeContainer = Source.CreateCloudBlobClient().GetContainerReference(ReadMeContainerName);

                MaxTryCount = DefaultMaxRetryCount;
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Failed to initalize job! {exception}");
                return false;
            }

            return true;
        }

        protected string TempDirectory;

        public override async Task<bool> Run()
        {
            // Grab package edits
            IList<PackageEdit> edits;
            Trace.TraceInformation($"Fetching queued edits from {PackageDatabase.DataSource}/{PackageDatabase.InitialCatalog}");
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

            Trace.TraceInformation("Fetched {2} queued edits from {0}/{1}", PackageDatabase.DataSource, PackageDatabase.InitialCatalog, edits.Count);

            // Group by package and take just the most recent edit for each package
            edits = edits
                .GroupBy(e => e.PackageKey)
                .Select(g => g.OrderByDescending(e => e.Timestamp).FirstOrDefault())
                .Where(e => e != null)
                .ToList();

            // Process packages
            foreach (var edit in edits)
            {
                Trace.TraceInformation($"Editing {edit.Id} {edit.Version}");
                try
                {
                    await ApplyEdit(edit);
                    Trace.TraceInformation($"Edited {edit.Id} {edit.Version}");
                }
                catch (Exception exception)
                {
                    Trace.TraceError($"Error editing package {edit.Id} {edit.Version} (try {edit.TriedCount + 1} / {MaxTryCount})! {exception}");
                    await UpdatePackageEditDbWithError(exception, edit.Key);
                }
            }
            return true;
        }

        private async Task ApplyEdit(PackageEdit edit)
        {
            string originalPath = null;
            string originalReadMeMDPath = null;
            string originalReadMeHTMLPath = null;

            try
            {
                TempDirectory = Path.Combine(Path.GetTempPath(), "NuGetService", "HandlePackageEdits");
                var directory = Path.Combine(TempDirectory, edit.Id, edit.Version);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                originalPath = Path.Combine(directory, "original.nupkg");
                Trace.TraceInformation($"Downloading nupkg to {originalPath}");

                var sourceBlob = SourceContainer.GetBlockBlobReference(
                    StorageHelpers.GetPackageBlobName(edit.Id, edit.Version));
                Trace.TraceInformation($"Name is {sourceBlob.Name}, storage uri is {sourceBlob.StorageUri}");

                // Download the original file
                Trace.TraceInformation($"Downloading original copy of {edit.Id} {edit.Version}");
                await sourceBlob.DownloadToFileAsync(originalPath, FileMode.Create);
                Trace.TraceInformation($"Downloaded original copy of {edit.Id} {edit.Version}");

                // Check that a backup exists
                var backupBlob = BackupsContainer.GetBlockBlobReference(
                    StorageHelpers.GetPackageBackupBlobName(edit.Id, edit.Version, edit.Hash));
                if (!await backupBlob.ExistsAsync())
                {
                    Trace.TraceInformation($"Backing up original copy of {edit.Id} {edit.Version}");
                    await backupBlob.UploadFromFileAsync(originalPath);
                    Trace.TraceInformation($"Backed up original copy of {edit.Id} {edit.Version}");
                }

                // Update the nupkg manifest with the new metadata
                using (var originalStream = File.Open(originalPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    Trace.TraceInformation($"Rewriting package file for {edit.Id} {edit.Version}");

                    var editActionList = edit.GetEditsAsActionList();
                    NupkgRewriter.RewriteNupkgManifest(originalStream, editActionList);

                    Trace.TraceInformation($"Rewrote package file for {edit.Id} {edit.Version}");
                }

                // Snapshot the original blob
                Trace.TraceInformation($"Snapshotting original blob for {edit.Id} {edit.Version} ({sourceBlob.Uri.AbsoluteUri}).");
                var sourceSnapshot = await sourceBlob.CreateSnapshotAsync();
                Trace.TraceInformation($"Snapshotted original blob for {edit.Id} {edit.Version} ({sourceBlob.Uri.AbsoluteUri}).");

                // Upload the updated file
                Trace.TraceInformation($"Uploading modified package file for {edit.Id} {edit.Version} to {sourceBlob.Uri.AbsoluteUri}");
                await sourceBlob.UploadFromFileAsync(originalPath);
                Trace.TraceInformation($"Uploaded modified package file for {edit.Id} {edit.Version} to {sourceBlob.Uri.AbsoluteUri}");

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
                
                // ReadMe update
                var readMeMDBlob = await UpdateReadMeAsync(edit, directory, originalReadMeMDPath, MarkdownExtension);

                var readMeHTMLBlob = await UpdateReadMeAsync(edit, directory, originalReadMeHTMLPath, HtmlExtension);

                try
                {
                    Trace.TraceInformation($"Updating package record for {edit.Id} {edit.Version}");
                    await UpdateDatabaseWithEdit(edit, hash, size);
                    Trace.TraceInformation($"Updated package record for {edit.Id} {edit.Version}");
                }
                catch (Exception exception)
                {
                    // Error occurred while updaing database, roll back the blob to the snapshot
                    Trace.TraceError($"Failed to update database! {exception}");
                    Trace.TraceWarning(
                        $"Rolling back updated blob for {edit.Id} {edit.Version}. Copying snapshot {sourceSnapshot.Uri.AbsoluteUri} to {sourceBlob.Uri.AbsoluteUri}");
                    await sourceBlob.StartCopyAsync(sourceSnapshot);
                    while (sourceBlob.CopyState.Status != CopyStatus.Success)
                    {
                        await Task.Delay(1000);
                    }
                    Trace.TraceWarning(
                        $"Rolled back updated blob for {edit.Id} {edit.Version}. Copying snapshot {sourceSnapshot.Uri.AbsoluteUri} to {sourceBlob.Uri.AbsoluteUri}");

                    await RollBackReadMeAsync(edit, directory, originalReadMeMDPath, readMeMDBlob.activeSnapshot, readMeMDBlob.activeBlob);
                    await RollBackReadMeAsync(edit, directory, originalReadMeHTMLPath, readMeHTMLBlob.activeSnapshot, readMeHTMLBlob.activeBlob);

                    throw;
                }

                if (edit.ReadMeState == ReadMeChanged)
                {
                    // Delete pending ReadMes
                    Trace.TraceInformation($"Deleting pending ReadMe for {edit.Id} {edit.Version} from {readMeMDBlob.pendingBlob.Uri.AbsoluteUri}");
                    await readMeMDBlob.pendingBlob.DeleteIfExistsAsync();
                    Trace.TraceInformation($"Deleted pending ReadMe for {edit.Id} {edit.Version} from {readMeMDBlob.pendingBlob.Uri.AbsoluteUri}");

                    Trace.TraceInformation($"Deleting pending ReadMe for {edit.Id} {edit.Version} from {readMeHTMLBlob.pendingBlob.Uri.AbsoluteUri}");
                    await readMeHTMLBlob.pendingBlob.DeleteIfExistsAsync();
                    Trace.TraceInformation($"Deleted pending ReadMe for {edit.Id} {edit.Version} from {readMeHTMLBlob.pendingBlob.Uri.AbsoluteUri}");
                }

                Trace.TraceInformation("Deleting snapshot blob {2} for {0} {1}.", edit.Id, edit.Version, sourceSnapshot.Uri.AbsoluteUri);
                await sourceSnapshot.DeleteAsync();
                Trace.TraceInformation("Deleted snapshot blob {2} for {0} {1}.", edit.Id, edit.Version, sourceSnapshot.Uri.AbsoluteUri);

                if (readMeMDBlob.activeSnapshot != null)
                {
                    Trace.TraceInformation("Deleting snapshot ReadMe {2} for {0} {1}.", edit.Id, edit.Version, readMeMDBlob.activeSnapshot.Uri.AbsoluteUri);
                    await readMeMDBlob.activeSnapshot.DeleteAsync();
                    Trace.TraceInformation("Deleted snapshot ReadMe{2} for {0} {1}.", edit.Id, edit.Version, readMeMDBlob.activeSnapshot.Uri.AbsoluteUri);
                }

                if (readMeHTMLBlob.activeSnapshot != null)
                {
                    Trace.TraceInformation("Deleting snapshot ReadMe {2} for {0} {1}.", edit.Id, edit.Version, readMeHTMLBlob.activeSnapshot.Uri.AbsoluteUri);
                    await readMeHTMLBlob.activeSnapshot.DeleteAsync();
                    Trace.TraceInformation("Deleted snapshot ReadMe {2} for {0} {1}.", edit.Id, edit.Version, readMeHTMLBlob.activeSnapshot.Uri.AbsoluteUri);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalPath) && File.Exists(originalPath))
                {
                    File.Delete(originalPath);
                }
            }
        }

        private async Task UpdateDatabaseWithEdit(PackageEdit edit, string hash, long size)
        {
            // insert missing authors as empty in authors table for consistency with gallery
            // scenario is metadata edit during verification of package uploaded without authors
            if (string.IsNullOrWhiteSpace(edit.Authors))
            {
                edit.Authors = string.Empty;
            }

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
                    edit.RepositoryUrl,
                    edit.ReadMeState,
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

                // Update parameters with new HasReadMe value
                var HasReadMeSQL = string.Empty;
                if (edit.ReadMeState == ReadMeChanged)
                {
                    parameters.Add("HasReadMe", 1);
                    HasReadMeSQL = ", HasReadMe = @HasReadMe";

                }
                else if (edit.ReadMeState == ReadMeDeleted)
                {
                    parameters.Add("HasReadMe", 0);
                    HasReadMeSQL = ", HasReadMe = @HasReadMe";
                }

                // Prep SQL for merging in authors
                var loadAuthorsSql = new StringBuilder();
                var authors = edit.Authors.Split(',');
                for (var i = 0; i < authors.Length; i++)
                {
                    loadAuthorsSql.Append("INSERT INTO [PackageAuthors]([PackageKey],[Name]) VALUES(@PackageKey, @Author" + i + ")");
                    parameters.Add("Author" + i, authors[i]);
                }

                await connection.QueryAsync<int>($@"
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
                                            Published,
                                            RepositoryUrl
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
                                        FlattenedAuthors = @Authors,
                                        RepositoryUrl = @RepositoryUrl
                                        {HasReadMeSQL}
                                WHERE   [Key] = @PackageKey

                                -- Update Authors
                                DELETE FROM [PackageAuthors]
                                WHERE PackageKey = @PackageKey

                                " + loadAuthorsSql + @"

                                -- Clean this edit and all previous edits.
                                DELETE FROM [PackageEdits]
                                WHERE [PackageKey] = @PackageKey
                                AND [Key] <= @Key
                            " + "COMMIT TRANSACTION",
                    parameters);
            }
        }

        private async Task UpdatePackageEditDbWithError(Exception exception, int editKey)
        {
            try
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
                        error = exception.ToString(),
                        key = editKey
                    });
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error updating the package edit database with error! {ex}");
            }
        }

        private async Task<ReadMeBlobs> UpdateReadMeAsync(PackageEdit edit, 
            string directory, string originalReadMePath, string readMeExtension)
        {
            ReadMeBlobs currentBlob = new ReadMeBlobs();

            // ReadMeState of null means unmodified
            if (edit.ReadMeState == null)
            {
                return currentBlob;
            }

            originalReadMePath = Path.Combine(directory, "readme" + readMeExtension);
            Trace.TraceInformation($"Attempting to save ReadMe at {originalReadMePath}");

            currentBlob.activeBlob = ReadMeContainer.GetBlockBlobReference(
                        StorageHelpers.GetActiveReadMeBlobNamePath(edit.Id, edit.Version, readMeExtension));
            Trace.TraceInformation($"Found active ReadMe {currentBlob.activeBlob.Name} at storage URI {currentBlob.activeBlob.StorageUri}");

            // Update ReadMe in blob storage
            if (edit.ReadMeState == ReadMeChanged)
            {
                // Do the blob update
                try
                {
                    currentBlob.pendingBlob = ReadMeContainer.GetBlockBlobReference(
                        StorageHelpers.GetPendingReadMeBlobNamePath(edit.Id, edit.Version, readMeExtension));
                    Trace.TraceInformation($"Found pending ReadMe {currentBlob.pendingBlob.Name} at storage URI {currentBlob.pendingBlob.StorageUri}");

                    if (edit.HasReadMe)
                    {
                        // Snapshot the original blob if it exists (so if it's an edit, not an upload)
                        Trace.TraceInformation($"Snapshotting original blob for {edit.Id} {edit.Version} ({currentBlob.activeBlob.Uri.AbsoluteUri}).");
                        currentBlob.activeSnapshot = await currentBlob.activeBlob.CreateSnapshotAsync();
                        Trace.TraceInformation($"Snapshotted original blob for {edit.Id} {edit.Version} ({currentBlob.activeBlob.Uri.AbsoluteUri}).");
                    }

                    // Download pending ReadMe
                    Trace.TraceInformation($"Downloading new ReadMe for {edit.Id} {edit.Version}");
                    await currentBlob.pendingBlob.DownloadToFileAsync(originalReadMePath, FileMode.Create);
                    Trace.TraceInformation($"Downloaded new ReadMe for {edit.Id} {edit.Version}");

                    // Upload pending ReadMe to active
                    Trace.TraceInformation($"Uploading new ReadMe for {edit.Id} {edit.Version} to {currentBlob.activeBlob.Uri.AbsoluteUri}");
                    await currentBlob.activeBlob.UploadFromFileAsync(originalReadMePath);
                    Trace.TraceInformation($"Uploaded new ReadMe for {edit.Id} {edit.Version} to {currentBlob.activeBlob.Uri.AbsoluteUri}");
                }
                finally
                {
                    if (!string.IsNullOrEmpty(originalReadMePath) && File.Exists(originalReadMePath))
                    {
                        File.Delete(originalReadMePath);
                    }
                }
            }
            // Delete ReadMe in blob storage
            else if (edit.ReadMeState == ReadMeDeleted)
            {
                // Download active ReadMe
                Trace.TraceInformation($"Downloading old ReadMe for {edit.Id} {edit.Version}");
                await currentBlob.activeBlob.DownloadToFileAsync(originalReadMePath, FileMode.Create);
                Trace.TraceInformation($"Downloaded old ReadMe for {edit.Id} {edit.Version}");

                // Delete active ReadMe
                Trace.TraceInformation($"Deleting ReadMe of {edit.Id} {edit.Version} from {currentBlob.activeBlob.Uri.AbsoluteUri}");
                await currentBlob.activeBlob.DeleteIfExistsAsync();
                Trace.TraceInformation($"Deleted ReadMe of {edit.Id} {edit.Version} from {currentBlob.activeBlob.Uri.AbsoluteUri}");
            }
            return currentBlob;
        }

        private async Task RollBackReadMeAsync(PackageEdit edit, string directory, string originalReadMePath, 
            CloudBlockBlob activeReadMeSnapshot, CloudBlockBlob activeReadMeBlob)
        {
            if (edit.ReadMeState != null)
            {
                if (edit.ReadMeState == ReadMeChanged)
                {
                    if (edit.HasReadMe)
                    {
                        Trace.TraceWarning(
                        $"Rolling back ReadMe blob for {edit.Id} {edit.Version}. Copying snapshot {activeReadMeSnapshot.Uri.AbsoluteUri} to {activeReadMeBlob.Uri.AbsoluteUri}");
                        activeReadMeBlob.StartCopy(activeReadMeSnapshot);
                        while (activeReadMeBlob.CopyState.Status != CopyStatus.Success)
                        {
                            await Task.Delay(1000);
                        }
                        Trace.TraceWarning(
                            $"Rolled back ReadMe blob for {edit.Id} {edit.Version}. Copying snapshot {activeReadMeSnapshot.Uri.AbsoluteUri} to {activeReadMeBlob.Uri.AbsoluteUri}");
                    }
                    else
                    {
                        // Delete ReadMes from active
                        Trace.TraceInformation($"Deleting ReadMe of {edit.Id} {edit.Version} from {activeReadMeBlob.Uri.AbsoluteUri}");
                        await activeReadMeBlob.DeleteIfExistsAsync();
                        Trace.TraceInformation($"Deleted ReadMe of {edit.Id} {edit.Version} from {activeReadMeBlob.Uri.AbsoluteUri}");
                    }
                }
                else if (edit.ReadMeState == ReadMeDeleted)
                {
                    try
                    {
                        // Upload original ReadMe back to active
                        Trace.TraceInformation($"Uploading old ReadMe for {edit.Id} {edit.Version} to {activeReadMeBlob.Uri.AbsoluteUri}");
                        await activeReadMeBlob.UploadFromFileAsync(originalReadMePath);
                        Trace.TraceInformation($"Uploaded old ReadMe for {edit.Id} {edit.Version} to {activeReadMeBlob.Uri.AbsoluteUri}");
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(originalReadMePath) && File.Exists(originalReadMePath))
                        {
                            File.Delete(originalReadMePath);
                        }
                    }
                }
            }
        }
    }
}
