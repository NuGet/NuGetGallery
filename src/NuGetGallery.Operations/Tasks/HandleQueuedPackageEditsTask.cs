using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.EntityClient;
using System.Data.Objects;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
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
            var editsPerPackage = entitiesContext.Set<PackageEdit>()
                .Where(pe => pe.TriedCount < 3)
                .Include(pe => pe.Package)
                .Include(pe => pe.Package.PackageRegistration)
                .Include(pe => pe.Package.User)
                .ToList()
                .GroupBy(pe => pe.PackageKey);

            // Do edit with a 'most recent edit to this package wins - other edits are deleted' strategy.
            // Not doing editing in parallel because
            // a) any particular blob may use a large amount of memory to process. Let's not multiply that!
            // b) we don't want multithreaded usage of the entitiesContext (and its implied transactions)!
            foreach (IGrouping<int, PackageEdit> editsGroup in editsPerPackage)
            {
                ProcessPackageEdits(editsGroup, entitiesContext);
            }
        }

        /// <summary>
        /// Creates an archived copy of the original package blob if it doesn't already exist.
        /// </summary>
        private void ArchiveOriginalPackageBlob(CloudBlockBlob originalPackageBlob, CloudBlockBlob latestPackageBlob)
        {
            // Copy the blob to backup only if it isn't already successfully copied
            if ((!originalPackageBlob.Exists()) || (originalPackageBlob.CopyState != null && originalPackageBlob.CopyState.Status != CopyStatus.Success))
            {
                if (!WhatIf)
                {
                    Log.Info("Backing up blob: {0} to {1}", latestPackageBlob.Name, originalPackageBlob.Name);
                    originalPackageBlob.StartCopyFromBlob(latestPackageBlob);
                    CopyState state = originalPackageBlob.CopyState;

                    for (int i = 0; (state == null || state.Status == CopyStatus.Pending) && i < SleepTimes.Length; i++)
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
        }

        private void ProcessPackageEdits(IEnumerable<PackageEdit> editsForThisPackage, EntitiesContext entitiesContext)
        {
            // List of Work to do:
            // 1) Backup old blob, if the original has not been backed up yet
            // 2) Downloads blob, create new NUPKG locally
            // 3) Upload blob
            // 4) Update the database
            PackageEdit edit = editsForThisPackage.OrderByDescending(pe => pe.Timestamp).First();

            var blobClient = StorageAccount.CreateCloudBlobClient();
            var packagesContainer = Util.GetPackagesBlobContainer(blobClient);

            var latestPackageFileName = Util.GetPackageFileName(edit.Package.PackageRegistration.Id, edit.Package.Version);
            var originalPackageFileName = Util.GetBackupOfOriginalPackageFileName(edit.Package.PackageRegistration.Id, edit.Package.Version);

            var originalPackageBackupBlob = packagesContainer.GetBlockBlobReference(originalPackageFileName);
            var latestPackageBlob = packagesContainer.GetBlockBlobReference(latestPackageFileName);

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
                        String.Format("Something went terribly wrong, only one entity should be updated but actually {0} entities were updated", nr));
                }
            }

            ArchiveOriginalPackageBlob(originalPackageBackupBlob, latestPackageBlob);
            using (var readWriteStream = new MemoryStream())
            {
                // Download to memory
                CloudBlockBlob downloadSourceBlob = WhatIf ? latestPackageBlob : originalPackageBackupBlob;
                Log.Info("Downloading original package blob to memory {0}", downloadSourceBlob.Name);
                downloadSourceBlob.DownloadToStream(readWriteStream);

                // Rewrite in memory
                Log.Info("Rewriting nupkg package in memory", downloadSourceBlob.Name);
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
                    var blobSnapshot = latestPackageBlob.CreateSnapshot();

                    // Start Transaction: Complete the edit in the gallery DB.
                    // Use explicit SQL transactions instead of EF operation-grouping 
                    // so that we can manually roll the transaction back on a blob related failure.
                    ObjectContext objectContext = (entitiesContext as IObjectContextAdapter).ObjectContext;
                    ((objectContext.Connection) as EntityConnection).Open(); // must open in order to begin transaction
                    using (EntityTransaction transaction = ((objectContext.Connection) as EntityConnection).BeginTransaction())
                    {
                        edit.Apply(hashAlgorithm: "SHA512", hash: newHash, packageFileSize: newPackageFileSize);

                        // Add to transaction: delete all the pending edits of this package.
                        foreach (var eachEdit in editsForThisPackage)
                        {
                            entitiesContext.DeleteOnCommit(eachEdit);
                        }

                        entitiesContext.SaveChanges(); // (transaction is still not committed, but do some EF legwork up-front of modifying the blob)
                        try
                        {
                            // Reupload blob
                            Log.Info("Uploading blob from memory {0}", latestPackageBlob.Name);
                            readWriteStream.Position = 0;
                            latestPackageBlob.UploadFromStream(readWriteStream);
                        }
                        catch (Exception e)
                        {
                            // Uploading the updated nupkg failed.
                            // Rollback the transaction, which restores the Edit to PackageEdits so it can be attempted again.
                            Log.Error("(error) - package edit blob update failed. Rolling back the DB transaction.");
                            Log.ErrorException("(exception", e);
                            Log.Error("(note) - blob snapshot URL = " + blobSnapshot.Uri);
                            transaction.Rollback();
                            return;
                        }

                        try
                        {
                            transaction.Commit();
                        }
                        catch (Exception e)
                        {
                            // Commit changes to DB failed.
                            // Since our blob update wasn't part of the transaction (and doesn't AFAIK have a 'commit()' operator we can utilize for the type of blobs we are using)
                            // try, (single attempt) to roll back the blob update by restoring the previous snapshot.
                            Log.Error("(error) - package edit DB update failed. Trying to roll back the blob to its previous snapshot.");
                            Log.ErrorException("(exception", e);
                            Log.Error("(note) - blob snapshot URL = " + blobSnapshot.Uri);
                            try
                            {
                                latestPackageBlob.StartCopyFromBlob(blobSnapshot);
                            }
                            catch (Exception e2)
                            {
                                // In this case it may not be the end of the world - the package metadata mismatches the edit now, 
                                // but there's still an edit in the queue, waiting to be rerun and put everything back in synch.
                                Log.Error("(error) - rolling back the package blob to its previous snapshot failed.");
                                Log.ErrorException("(exception", e2);
                                Log.Error("(note) - blob snapshot URL = " + blobSnapshot.Uri);
                            }
                        }
                    }
                }
            }
        }
    }
} 