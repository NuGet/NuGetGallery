// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace NuGet.Canton
{
    public class CatalogPageJob : QueueFedJob
    {
        private CloudStorageAccount _packagesStorageAccount;
        private ConcurrentQueue<Task> _queueTasks;
        private DirectoryInfo _tmpDir;
        private ConcurrentDictionary<int, bool> _workQueueStatus;

        public CatalogPageJob(Config config, Storage storage, string queueName)
            : base(config, storage, queueName)
        {
            _packagesStorageAccount = CloudStorageAccount.Parse(config.GetProperty("PackagesStorageConnectionString"));
            _queueTasks = new ConcurrentQueue<Task>();
            _workQueueStatus = new ConcurrentDictionary<int, bool>();

            _tmpDir = new DirectoryInfo(Config.GetProperty("localtmp"));
            if (!_tmpDir.Exists)
            {
                _tmpDir.Create();
            }
        }

        public override async Task RunCore(CancellationToken cancellationToken)
        {
            TimeSpan hold = TimeSpan.FromMinutes(90);

            var messages = Queue.GetMessages(32, hold).ToList();

            var qClient = Account.CreateCloudQueueClient();
            var queue = qClient.GetQueueReference(CantonConstants.CatalogPageQueue);

            while (messages.Count > 0)
            {
                foreach (var message in messages)
                {
                    JObject work = JObject.Parse(message.AsString);
                    Uri galleryPageUri = new Uri(work["uri"].ToString());
                    int cantonCommitId = work["cantonCommitId"].ToObject<int>();
                    Log("started cantonCommitId: " + cantonCommitId);

                    try
                    {
                        // read the gallery page
                        JObject galleryPage = await GetJson(galleryPageUri);

                        // graph modififactions
                        GraphAddon[] addons = new GraphAddon[0];
                        //GraphAddon[] addons = new GraphAddon[] { 
                        //        new OriginGraphAddon(galleryPageUri.AbsoluteUri, cantonCommitId),
                        //        new GalleryGraphAddon(galleryPage)
                        //    };

                        string id = galleryPage["id"].ToString();
                        string version = galleryPage["version"].ToString();

                        DateTime? published = null;
                        JToken publishedToken = null;
                        if (galleryPage.TryGetValue("published", out publishedToken))
                        {
                            published = publishedToken.ToObject<DateTime>();
                        }

                        // download the nupkg
                        FileInfo nupkg = await GetNupkg(id, version);

                        Action<Uri> handler = (resourceUri) => QueuePage(resourceUri, Schema.DataTypes.PackageDetails, cantonCommitId, queue);

                        // create the new catalog item
                        using (var stream = nupkg.OpenRead())
                        {
                            // Create the core catalog page graph and upload it
                            using (CatalogPageCreator writer = new CatalogPageCreator(Storage, handler, addons))
                            {
                                CatalogItem catalogItem = Utils.CreateCatalogItem(stream, published, null, nupkg.FullName);
                                writer.Add(catalogItem);
                                await writer.Commit(DateTime.UtcNow, null, cancellationToken);
                            }
                        }

                        // clean up
                        nupkg.Delete();
                    }
                    catch (Exception ex)
                    {
                        LogError("Unable to build catalog page for: " + galleryPageUri.AbsoluteUri + " Error: " + ex.ToString());
                    }
                    finally
                    {
                        _queueTasks.Enqueue(Queue.DeleteMessageAsync(message));

                        bool status = false;
                        if (!_workQueueStatus.TryRemove(cantonCommitId, out status) || status == false)
                        {
                            // we have to send something, the job on the other side should recognize https://failed/
                            QueuePage(new Uri("https://failed/"), Schema.DataTypes.PackageDetails, cantonCommitId, queue);

                            LogError("Unable to build catalog page for: " + galleryPageUri.AbsoluteUri);
                        }
                    }
                }

                // get the next work item
                if (_run)
                {
                    messages = Queue.GetMessages(32, hold).ToList();
                }
                else
                {
                    messages = new List<CloudQueueMessage>();
                }

                // let deletes catch up
                Task task = null;
                while (_queueTasks.TryDequeue(out task))
                {
                    task.Wait();
                }
            }

            // final wait
            Task curTask = null;
            while (_queueTasks.TryDequeue(out curTask))
            {
                curTask.Wait();
            }
        }

        private void QueuePage(Uri resourceUri, Uri itemType, int cantonCommitId, CloudQueue queue)
        {
            // mark that we sent a message for this
            _workQueueStatus.AddOrUpdate(cantonCommitId, true, (k,v) => true);

            JObject summary = new JObject();
            summary.Add("uri", resourceUri.AbsoluteUri);
            summary.Add("itemType", itemType.AbsoluteUri);
            summary.Add("submitted", DateTime.UtcNow.ToString("O"));
            summary.Add("failures", 0);
            summary.Add("host", Host);
            summary.Add("cantonCommitId", cantonCommitId);

            _queueTasks.Enqueue(queue.AddMessageAsync(new CloudQueueMessage(summary.ToString())));

            Log("Page Creation: Commit: " + cantonCommitId + " Uri: " + resourceUri.AbsoluteUri);
        }

        private async Task<JObject> GetJson(Uri uri)
        {
            var blobClient = Account.CreateCloudBlobClient();
            var galleryBlob = blobClient.GetBlobReferenceFromServer(uri);

            JObject json = null;

            using (MemoryStream stream = new MemoryStream())
            {
                await galleryBlob.DownloadToStreamAsync(stream);
                stream.Seek(0, SeekOrigin.Begin);

                using (StreamReader reader = new StreamReader(stream))
                {
                    string s = reader.ReadToEnd();
                    json = JObject.Parse(s);
                }
            }

            return json;
        }

        private async Task<FileInfo> GetNupkg(string id, string version)
        {
            // TODO: Use the MD5 hash on the prod packgae to see if we have the current one

            //NuGetVersion nugetVersion = new NuGetVersion(version);

            var tmpBlobClient = Account.CreateCloudBlobClient();

            string packageName = String.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", id, version).ToLowerInvariant();

            FileInfo file = new FileInfo(Path.Combine(_tmpDir.FullName, Guid.NewGuid().ToString() + ".nupkg"));

            var tmpContainer = tmpBlobClient.GetContainerReference(Config.GetProperty("tmp"));

            string tmpFile = String.Format(CultureInfo.InvariantCulture, "packages/{0}", packageName);
            var tmpBlob = tmpContainer.GetBlockBlobReference(tmpFile);

            if (await tmpBlob.ExistsAsync())
            {
                Log("Downloading from tmp: " + packageName);
                await tmpBlob.DownloadToFileAsync(file.FullName, FileMode.CreateNew);
            }
            else
            {
                var blobClient = _packagesStorageAccount.CreateCloudBlobClient();
                var prodPackages = blobClient.GetContainerReference("packages");
                var prodBlob = prodPackages.GetBlockBlobReference(packageName);

                if (prodBlob.Exists())
                {
                    Log("Downloading from prod: " + packageName);

                    await prodBlob.DownloadToFileAsync(file.FullName, FileMode.CreateNew);

                    // store this in tmp also
                    await tmpBlob.UploadFromFileAsync(file.FullName, FileMode.Open);
                }
                else
                {
                    throw new FileNotFoundException(prodBlob.Uri.AbsoluteUri);
                }
            }

            return file;
        }
    }
}
