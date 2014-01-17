using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Configuration;
using NuGet.Services.IO;
using NuGet.Services.Storage;
using NuGet.Services.Work.Jobs.Models;

namespace NuGet.Services.Work.Jobs
{
    [Description("Handles pending package edits")]
    public class HandlePackageEditsJob : JobHandler<QueuePackageEditsEventSource>
    {
        public static readonly long DefaultMaxAllowedManifestBytes = 10 /* Mb */ * 1024 /* Kb */ * 1024; /* b */

        public static readonly string GetEditsBaseSql = @"
            SELECT pr.Id, p.NormalizedVersion AS Version, p.Hash, e.*
            FROM PackageEdits e
            INNER JOIN Packages p ON p.[Key] = e.PackageKey
            INNER JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey";

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the source for package blobs
        /// </summary>
        public AzureStorageReference Source { get; set; }

        /// <summary>
        /// Gets or sets a connection string to the database containing package data.
        /// </summary>
        public SqlConnectionStringBuilder PackageDatabase { get; set; }

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the backup storage for package blobs
        /// </summary>
        public AzureStorageReference Backups { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of tries that are allowed before considering an edit failed.
        /// </summary>
        public int? MaxTryCount { get; set; }

        public long? MaxAllowedManifestBytes { get; set; }

        protected CloudBlobContainer SourceContainer { get; set; }
        protected CloudBlobContainer BackupsContainer { get; set; }

        protected ConfigurationHub Config { get; set; }
        protected long MaxManifestSize { get; set; }
        
        public HandlePackageEditsJob(ConfigurationHub config)
        {
            Config = config;
        }

        protected internal override async Task Execute()
        {
            // Load defaults
            MaxManifestSize = MaxAllowedManifestBytes ?? DefaultMaxAllowedManifestBytes;
            PackageDatabase = PackageDatabase ?? Config.Sql.GetConnectionString(KnownSqlServer.Primary);
            var sourceAccount = Source == null ?
                Storage.Legacy :
                Storage.GetAccount(Source);
            var backupsAccount = Backups== null ?
                Storage.Backup :
                Storage.GetAccount(Backups);
            SourceContainer = sourceAccount.Blobs.Client.GetContainerReference(
                Source == null ? PackageHelpers.PackageBlobContainer : Source.Container);
            BackupsContainer = backupsAccount.Blobs.Client.GetContainerReference(
                Backups == null ? PackageHelpers.BackupsBlobContainer : Backups.Container);
            
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
                await ApplyEdit(edit);
                Log.EditedPackage(edit.Id, edit.Version);
            }
        }

        private static readonly Regex ManifestSelector = new Regex(@"^[^/]*\.nuspec$", RegexOptions.IgnoreCase);
        private async Task ApplyEdit(PackageEdit edit)
        {
            // Download the original file
            string originalPath = Path.Combine(TempDirectory, "original.nupkg");
            var sourceBlob = SourceContainer.GetBlockBlobReference(
                PackageHelpers.GetPackageBlobName(edit.Id, edit.Version));
            Log.DownloadingOriginal(edit.Id, edit.Version);
            await sourceBlob.DownloadToFileAsync(originalPath, FileMode.Create);
            Log.DownloadedOriginal(edit.Id, edit.Version);

            // Check that a backup exists
            var backupBlob = BackupsContainer.GetBlockBlobReference(
                PackageHelpers.GetPackageBlobName(edit.Id, edit.Version, edit.Hash));
            if (!await backupBlob.ExistsAsync())
            {
                await backupBlob.UploadFromFileAsync(originalPath, FileMode.Open);
            }

            // Load the zip file and find the manifest
            using (var originalStream = File.Open(originalPath, FileMode.Open, FileAccess.ReadWrite))
            {
                ZipArchive archive = new ZipArchive(originalStream);
                
                // Find the nuspec
                var nuspecEntries = archive.Entries.Where(e => ManifestSelector.IsMatch(e.FullName)).ToArray();
                if(nuspecEntries.Count == 0) {
                    throw new InvalidDataException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.HandlePackageEditsJob_MissingManifest,
                        edit.Id,
                        edit.Version,
                        backupBlob.Uri.AbsoluteUri));
                } else if(nuspecEntries.Count > 1) {
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
                using(var manifestStream = new MaxSizeStream(manifestEntry.Open(), Math.Min(manifestEntry.Length, MaxAllowedManifestBytes))) {
                Manifest manifest = Manifest.ReadFrom(manifestStream, validateSchema: false);

                // Modify the manifest as per the edit
                edit.ApplyTo(manifest.Metadata);

                // Save the manifest back
                    manifestStream.Seek(0, SeekOrigin.Begin);
                    manifestStream.SetLength(0);
                    manifest.Save(manifestStream);
                }
            }
        }
    }

    public class QueuePackageEditsEventSource : EventSource
    {
        public static readonly QueuePackageEditsEventSource Log = new QueuePackageEditsEventSource();
        private QueuePackageEditsEventSource() { }

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
        public void EditedPackage(string id, string version) { WriteEvent(4, id, version, invocation); }

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

        public static class Tasks
        {
            public const EventTask FetchingQueuedEdits = (EventTask)0x1;
            public const EventTask EditingPackage = (EventTask)0x2;
            public const EventTask DownloadingOriginal = (EventTask)0x3;
            public const EventTask BackingUpOriginal = (EventTask)0x4;
        }
    }
}
