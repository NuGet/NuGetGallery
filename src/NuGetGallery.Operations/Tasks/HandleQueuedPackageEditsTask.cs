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
                .ToList();

            ConcurrentDictionary<PackageEdit, CloudBlockBlob> blobCache = new ConcurrentDictionary<PackageEdit,CloudBlockBlob>();

            Parallel.ForEach(edits, new ParallelOptions { MaxDegreeOfParallelism = 10 }, edit =>
                {
                    var blob = BackupBlob(edit);
                    blobCache.TryAdd(edit, blob); //should always succeed 
                });

            // Not doing the actual editing in parallel because
            // a) any particular blob may use a large amount of memory to process. Let's not multiply that!
            // b) we don't want multithreaded SaveChanges on the entitiesContext!
            foreach (var edit in edits)
            {
                UpdateNupkgBlob(edit, blobCache[edit], entitiesContext);
            }
        }

        // Hack: in WhatIf mode, returns the original blob
        private CloudBlockBlob BackupBlob(PackageEdit edit)
        {
            CloudStorageAccount storageAccount = CurrentEnvironment.MainStorage;
            var blobClient = storageAccount.CreateCloudBlobClient();
            var packagesContainer = Util.GetPackagesBlobContainer(blobClient);

            var latestPackageFileName = Util.GetPackageFileName(edit.Package.PackageRegistration.Id, edit.Package.Version);
            var originalPackageFileName = Util.GetBackupOriginalPackageFileName(edit.Package.PackageRegistration.Id, edit.Package.Version);

            var originalPackageBlob = packagesContainer.GetBlockBlobReference(originalPackageFileName);
            var latestPackageBlob = packagesContainer.GetBlockBlobReference(latestPackageFileName);

            if (!originalPackageBlob.Exists())
            {
                if (WhatIf)
                {
                    return latestPackageBlob; // said hack
                }
                else
                {
                    Log.Info("Backing up blob: {0} to {1}", latestPackageFileName, originalPackageFileName);
                    originalPackageBlob.StartCopyFromBlob(latestPackageBlob);
                    CopyState state = originalPackageBlob.CopyState;
                    while (state == null || state.Status == CopyStatus.Pending)
                    {
                        Log.Info("(sleeping for a copy completion)");
                        Thread.Sleep(3000); 
                        originalPackageBlob.FetchAttributes(); // To get a refreshed x-ms-copy-status response header - according to my theoretical understanding

                        //refresh state
                        state = originalPackageBlob.CopyState;
                    }

                    if (state.Status != CopyStatus.Success)
                    {
                        throw new BlobBackupFailedException(string.Format("Blob copy failed: CopyState={0}", state.StatusDescription));
                    }
                }
            }

            return originalPackageBlob;
        }

        private void UpdateNupkgBlob(PackageEdit edit, CloudBlockBlob nupkgBlob, EntitiesContext entitiesContext)
        {
            // Work to do:
            // 1) Backup old blob, if it is an original
            // 2) Download blob, create new NUPKG locally
            // 3) Upload blob

            List<Action<ManifestMetadata>> edits = new List<Action<ManifestMetadata>>
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
                using (var readWriteStream = new MemoryStream())
                {
                    edit.TriedCount += 1;
                    int nr = entitiesContext.SaveChanges();
                    if (nr != 1)
                    {
                        throw new ApplicationException("Something went terribly wrong, only one entity should be updated but actually " + nr + "entity(ies) were updated");
                    }

                    // Snapshot the blob
                    var snapshotBlob = nupkgBlob.CreateSnapshot();

                    // Download to memory
                    Log.Info("Downloading blob to memory {0}", nupkgBlob.Name);
                    nupkgBlob.DownloadToStream(readWriteStream);

                    // Rewrite in memory
                    Log.Info("Rewriting nupkg package in memory", nupkgBlob.Name);
                    NupkgRewriter.RewriteNupkgManifest(readWriteStream, edits);

                    // Get updated hash code, and file size
                    var newPackageFileSize = readWriteStream.Length;
                    var hashAlgorithm = HashAlgorithm.Create("SHA512");
                    byte[] hashBytes = hashAlgorithm.ComputeHash(readWriteStream.GetBuffer());
                    var newHash = Convert.ToBase64String(hashBytes);

                    // Start Transaction: Complete the edit in the gallery DB.
                    // Use explicit SQL transactions on the assumption EF SaveChanges() is doing it in some other way which doesn't
                    // support auto-rollback (how could it, since there's no explicit begin transaction?).
                    var transaction = entitiesContext.Database.Connection.BeginTransaction();
                    try
                    {
                        var package = edit.Package;
                        package.PackageHistories.Add(new PackageHistory(package));
                        edit.ApplyTo(package, hashAlgorithm: "SHA512", hash: newHash, packageFileSize: newPackageFileSize);
                        package.PackageEdits.Remove(edit);
                        entitiesContext.SaveChanges();

                        // Reupload blob
                        Log.Info("Uploading blob from memory {0}", nupkgBlob.Name);
                        readWriteStream.Position = 0;
                        nupkgBlob.UploadFromStream(readWriteStream);
                        try
                        {
                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            // Commit to database update failed. 
                            // Since our blob update wasn't really part of the transaction (and doesn't AFAIK have a 'commit()' operator we can utilize for the type of blobs we are using)
                            // try, (single attempt) to revert the blob update by restoring the previous snapshot.
                            Log.Error("(error) - package edit DB update failed. Trying to roll back the blob to its previous snapshot.");
                            Log.Error("(note) - blob snapshot URL = " + snapshotBlob.Uri);
                            nupkgBlob.StartCopyFromBlob(snapshotBlob);
                        }
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }

                    Log.Info("(success)");
                }
            }
        }

    }
} 