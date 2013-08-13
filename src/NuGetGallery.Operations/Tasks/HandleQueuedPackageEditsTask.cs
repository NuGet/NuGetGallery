using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery.Operations.Tasks
{
    [Command("handlequeuededits", "Handle Queued Package Edits", AltName = "hqe", MaxArgs = 0)]
    public class HandleQueuedPackageEditsTask : DatabaseAndStorageTask
    {
        static int[] SleepTimes = { 50, 750, 1500, 2500, 3750, 5250, 7000, 9000 };

        public override void ExecuteCommand()
        {
            // Work to do:
            // 0) Find Pending Edits in DB that have been attempted less than 3 times
            // 1) Backup all old NUPKGS
            // 2) Generate all new NUPKGs (in place), and tell gallery the edit is completed
            var connectionString = ConnectionString.ConnectionString;
            var storageAccount = StorageAccount;

            var entitiesContext = new EntitiesContext(connectionString, readOnly: false);
            var edits = entitiesContext.Set<PackageEdit>()
                .Where(pe => pe.TriedCount < 3)
                .Include(pe => pe.Package)
                .Include(pe => pe.Package.PackageRegistration)
                .Include(pe => pe.Package.User)
                .OrderBy(pe => pe.Timestamp) // Get PackageEdits in the order they were created.
                .ToList();

            // Not doing editing in parallel because
            // a) any particular blob may use a large amount of memory to process. Let's not multiply that!
            // b) we don't want multithreaded SaveChanges on the entitiesContext!
            // c) we [currently] might see multiple edits of a single package, and we want to back them up and apply them in the correct order.
            foreach (var edit in edits)
            {
                ProcessPackageEdit(edit, entitiesContext);
            }
        }

        /// <summary>
        /// Backups up the blob (original package blob) and returns the backup.
        /// note: in WhatIf mode, doesn't actually create a backup, and returns the original blob instead.
        /// </summary>
        private CloudBlockBlob BackupBlob(PackageEdit edit)
        {
            var blobClient = StorageAccount.CreateCloudBlobClient();
            var packagesContainer = Util.GetPackagesBlobContainer(blobClient);

            var latestPackageFileName = Util.GetPackageFileName(edit.Package.PackageRegistration.Id, edit.Package.Version);
            var originalPackageFileName = Util.GetBackupOfOriginalPackageFileName(edit.Package.PackageRegistration.Id, edit.Package.Version);

            var originalPackageBlob = packagesContainer.GetBlockBlobReference(originalPackageFileName);
            var latestPackageBlob = packagesContainer.GetBlockBlobReference(latestPackageFileName);

            if (!originalPackageBlob.Exists())
            {
                if (WhatIf)
                {
                    return latestPackageBlob; // returning original blob, NOT backup. Hopefully in WhatIf mode nobody will do anything destructive with that.
                }
                else
                {
                    Log.Info("Backing up blob: {0} to {1}", latestPackageFileName, originalPackageFileName);
                    AccessCondition whenblobDoesNotExist = AccessCondition.GenerateIfNoneMatchCondition("*");
                    originalPackageBlob.StartCopyFromBlob(latestPackageBlob, destAccessCondition: whenblobDoesNotExist);
                    CopyState state = originalPackageBlob.CopyState;

                    int i = 0;
                    while (state == null || state.Status == CopyStatus.Pending && i < SleepTimes.Length)
                    {
                        Log.Info("(sleeping for a copy completion)");
                        Thread.Sleep(SleepTimes[i]); 
                        originalPackageBlob.FetchAttributes(); // To get a refreshed CopyState

                        //refresh state
                        state = originalPackageBlob.CopyState;
                    }

                    if (state.Status != CopyStatus.Success)
                    {
                        string msg = string.Format("Blob copy failed: CopyState={0}", state.StatusDescription);
                        Log.Error("(error) " + msg);
                        throw new BlobBackupFailedException(msg);
                    }
                }
            }

            return originalPackageBlob;
        }

        private void ProcessPackageEdit(PackageEdit edit, EntitiesContext entitiesContext)
        {
            // List of Work to do:
            // 1) Backup old blob, if the original has not been backed up yet
            // 2) Downloads blob, create new NUPKG locally
            // 3) Upload blob
            // 4) Update the database
            var edits = new List<Action<ManifestMetadata>>
            { 
                (m) => { m.Authors = edit.Authors; },
                (m) => { m.Copyright = edit.Copyright; },
                (m) => { m.Description = edit.Description; },
                (m) => { m.IconUrl = edit.IconUrl; },
                (m) => { m.LicenseUrl = edit.LicenseUrl; },
                (m) => { m.ProjectUrl = edit.ProjectUrl; },
                (m) => { m.ReleaseNotes = edit.ReleaseNotes; },
                (m) => { m.RequireLicenseAcceptance = edit.RequiresLicenseAcceptance; },
                (m) => { m.Summary = edit.Summary; },
                (m) => { m.Title = edit.Title; },
                (m) => { m.Tags = edit.Tags; },
            };

            Log.Info(
                "Processing Edit Key={0}, PackageId={1}, Version={2}",
                edit.Key,
                edit.Package.PackageRegistration.Id,
                edit.Package.Version);
            
            if (!WhatIf)
            {
                edit.TriedCount += 1;
                int nr = entitiesContext.SaveChanges();
                if (nr != 1)
                {
                    throw new ApplicationException(
                        String.Format("Something went terribly wrong, only one entity should be updated but actually {0} entity(ies) were updated", nr));
                }
            }

            CloudBlockBlob nupkgBlob = BackupBlob(edit);
            using (var readWriteStream = new MemoryStream())
            {
                // Download to memory
                Log.Info("Downloading blob to memory {0}", nupkgBlob.Name);
                nupkgBlob.DownloadToStream(readWriteStream);

                // Rewrite in memory
                Log.Info("Rewriting nupkg package in memory", nupkgBlob.Name);
                NupkgRewriter.RewriteNupkgManifest(readWriteStream, edits);

                // Get updated hash code, and file size
                Log.Info("Computing updated hash code of memory stream");
                var newPackageFileSize = readWriteStream.Length;
                var hashAlgorithm = HashAlgorithm.Create("SHA512");
                byte[] hashBytes = hashAlgorithm.ComputeHash(readWriteStream.GetBuffer());
                var newHash = Convert.ToBase64String(hashBytes);

                if (!WhatIf)
                {
                    // Snapshot the blob
                    var snapshotBlob = nupkgBlob.CreateSnapshot();

                    // Start Transaction: Complete the edit in the gallery DB.
                    // Use explicit SQL transactions instead of EF operation-grouping 
                    // so that we can manually roll the transaction back on a blob related failure.
                    var transaction = entitiesContext.Database.Connection.BeginTransaction();
                    try
                    {
                        edit.Apply(hashAlgorithm: "SHA512", hash: newHash, packageFileSize: newPackageFileSize);
                        entitiesContext.SaveChanges();

                        // Reupload blob
                        Log.Info("Uploading blob from memory {0}", nupkgBlob.Name);
                        readWriteStream.Position = 0;
                        nupkgBlob.UploadFromStream(readWriteStream);
                        try
                        {
                            transaction.Commit();
                            Log.Info("(success)");
                        }
                        catch (Exception e)
                        {
                            // Commit to database update failed. 
                            // Since our blob update wasn't really part of the transaction (and doesn't AFAIK have a 'commit()' operator we can utilize for the type of blobs we are using)
                            // try, (single attempt) to revert the blob update by restoring the previous snapshot.
                            Log.Error("(error) - package edit DB update failed. Trying to roll back the blob to its previous snapshot.");
                            Log.ErrorException("(exception", e);
                            Log.Error("(note) - blob snapshot URL = " + snapshotBlob.Uri);
                            nupkgBlob.StartCopyFromBlob(snapshotBlob);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("(error) - package edit blob update failed. Rolling back the DB transaction.");
                        Log.ErrorException("(exception", e);
                        Log.Error("(note) - blob snapshot URL = " + snapshotBlob.Uri);
                        transaction.Rollback();
                    }
                }
            }
        }
    }
} 