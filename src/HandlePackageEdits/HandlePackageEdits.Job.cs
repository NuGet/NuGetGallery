using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
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
using NuGet;
using NuGet.Jobs;

namespace HandlePackageEdits
{
    internal class Job : JobBase
    {
        private const string HashAlgorithmName = "SHA512";
        public static readonly long DefaultMaxAllowedManifestBytes = 10 /* Mb */ * 1024 /* Kb */ * 1024; /* b */

        public static readonly string GetEditsBaseSql = @"
            SELECT pr.Id, p.NormalizedVersion AS Version, p.Hash, e.*
            FROM PackageEdits e
            INNER JOIN Packages p ON p.[Key] = e.PackageKey
            INNER JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey";

        public const string DefaultSourceContainerName = "packages";
        public const string DefaultBackupContainerName = "package-backups";

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
        protected long MaxManifestSize { get; set; }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            string maxManifestSizeString = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.MaxManifestSize);
            if (string.IsNullOrEmpty(maxManifestSizeString))
            {
                MaxManifestSize = DefaultMaxAllowedManifestBytes;
            }
            else
            {
                MaxManifestSize = Convert.ToInt64(maxManifestSizeString);
            }

            PackageDatabase = new SqlConnectionStringBuilder(
                        JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageDatabase));

            Source = CloudStorageAccount.Parse(
                                       JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SourceStorage));
            Backups = CloudStorageAccount.Parse(
                                       JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.BackupStorage));

            SourceContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.SourceContainerName) ?? DefaultSourceContainerName;
            BackupsContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.BackupContainerName) ?? DefaultBackupContainerName;

            SourceContainer = Source.CreateCloudBlobClient().GetContainerReference(SourceContainerName);
            BackupsContainer = Backups.CreateCloudBlobClient().GetContainerReference(BackupsContainerName);
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
                    Trace.TraceInformation($"Error editing package {edit.Id} {edit.Version}: {thrown.ToString()}");

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
                Trace.TraceInformation($"Edited {edit.Id} {edit.Version}");
            }
            return true;
        }

        private static readonly Regex ManifestSelector = new Regex(@"^[^/]*\.nuspec$", RegexOptions.IgnoreCase);

        private async Task ApplyEdit(PackageEdit edit)
        {
            // Download the original file
            string originalPath = null;
            TempDirectory = Path.Combine(Path.GetTempPath(), "NuGetService", "HandlePackageEdits");

            try
            {
                string directory = Path.Combine(TempDirectory, edit.Id, edit.Version);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                originalPath = Path.Combine(directory, "original.nupkg");
                var sourceBlob = SourceContainer.GetBlockBlobReference(
                    StorageHelpers.GetPackageBlobName(edit.Id, edit.Version));
                Trace.TraceInformation($"Name is {sourceBlob.Name}, storage uri is {sourceBlob.StorageUri}");
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

                // Load the zip file and find the manifest
                using (var originalStream = File.Open(originalPath, FileMode.Open, FileAccess.ReadWrite))
                using (var archive = new ZipArchive(originalStream, ZipArchiveMode.Update))
                {
                    // Find the nuspec
                    var nuspecEntries = archive.Entries.Where(e => ManifestSelector.IsMatch(e.FullName)).ToArray();
                    if (nuspecEntries.Length == 0)
                    {
                        throw new InvalidDataException(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.HandlePackageEditsJob_MissingManifest,
                            edit.Id,
                            edit.Version,
                            backupBlob.Uri.AbsoluteUri));
                    }
                    else if (nuspecEntries.Length > 1)
                    {
                        throw new InvalidDataException(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.HandlePackageEditsJob_MultipleManifests,
                            edit.Id,
                            edit.Version,
                            backupBlob.Uri.AbsoluteUri));
                    }

                    // We now have the nuspec
                    var manifestEntry = nuspecEntries.Single();

                    // Load the manifest with a constrained stream
                    Trace.TraceInformation($"Rewriting package file for {edit.Id} {edit.Version}");
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

                // Update the database
                try
                {
                    Trace.TraceInformation($"Updating package record for {edit.Id} {edit.Version}");
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
                        for (int i = 0; i < authors.Length; i++)
                        {
                            loadAuthorsSql.Append("INSERT INTO [PackageAuthors]([PackageKey],[Name]) VALUES(@PackageKey, @Author" + i + ")");
                            parameters.Add("Author" + i, authors[i]);
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

                                " + loadAuthorsSql + @"

                                -- Clean this edit and all previous edits.
                                DELETE FROM [PackageEdits]
                                WHERE [PackageKey] = @PackageKey
                                AND [Key] <= @Key
                            " +  "COMMIT TRANSACTION",
                            parameters);
                    }
                    Trace.TraceInformation($"Updated package record for {edit.Id} {edit.Version}");
                }
                catch (Exception)
                {
                    // Error occurred while updaing database, roll back the blob to the snapshot
                    // Can't do "await" in a catch block, but this should be pretty quick since it just starts the copy
                    Trace.TraceInformation(
                        $"Rolling back updated blob for {edit.Id} {edit.Version}. Copying snapshot {sourceSnapshot.Uri.AbsoluteUri} to {sourceBlob.Uri.AbsoluteUri}");
                    sourceBlob.StartCopy(sourceSnapshot);
                    Trace.TraceInformation(
                        $"Rolled back updated blob for {edit.Id} {edit.Version}. Copying snapshot {sourceSnapshot.Uri.AbsoluteUri} to {sourceBlob.Uri.AbsoluteUri}");
                    throw;
                }

                Trace.TraceInformation("Deleting snapshot blob {2} for {0} {1}.", edit.Id, edit.Version, sourceSnapshot.Uri.AbsoluteUri);
                await sourceSnapshot.DeleteAsync();
                Trace.TraceInformation("Deleted snapshot blob {2} for {0} {1}.", edit.Id, edit.Version, sourceSnapshot.Uri.AbsoluteUri);
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalPath) && File.Exists(originalPath))
                {
                    File.Delete(originalPath);
                }
            }
        }
      }
}
