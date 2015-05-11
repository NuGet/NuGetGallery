// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// A CollectorHttpClient that uses blob storage for requests instead of the CDN url.
    /// </summary>
    public class TransHttpClient : CollectorHttpClient
    {
        private readonly string _baseAddressInContent;
        private readonly CloudStorageAccount _account;
        private readonly string _azureBase;
        private readonly CloudBlobClient _blobClient;

        public TransHttpClient(CloudStorageAccount account)
            : this(account, null)
        {

        }

        public TransHttpClient(CloudStorageAccount account, string baseAddressInContent)
            : base()
        {
            _baseAddressInContent = baseAddressInContent;
            _account = account;
            _blobClient = account.CreateCloudBlobClient();
            _azureBase = _blobClient.BaseUri.AbsoluteUri;
        }

        public override Task<IGraph> GetGraphAsync(Uri address)
        {
            return base.GetGraphAsync(address);
        }

        public override async Task<JObject> GetJObjectAsync(Uri address)
        {
            var blob = GetBlob(address);

            if (blob == null)
            {
                return await base.GetJObjectAsync(address);
            }
            else
            {
                InReqCount();

                using (MemoryStream stream = new MemoryStream())
                {
                    var task = blob.DownloadToStreamAsync(stream);
                    task.Wait();
                    stream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new StreamReader(stream))
                    {
                        try
                        {
                            string json = reader.ReadToEnd();
                            return JObject.Parse(json);
                        }
                        catch (Exception e)
                        {
                            throw new Exception(string.Format("GetJObjectBlobAsync({0})", address), e);
                        }
                    }
                }
            }
        }

        public virtual ICloudBlob GetBlob(Uri uri)
        {
            string url = uri.AbsoluteUri;

            if (url.StartsWith(_baseAddressInContent))
            {
                string containerAndPath = url.Substring(_baseAddressInContent.Length);

                int pos = containerAndPath.IndexOf('/');

                if (pos > 0)
                {
                    string container = containerAndPath.Substring(0, pos);

                    var blobContainer = _blobClient.GetContainerReference(container);

                    if (containerAndPath.Length > (pos + 2))
                    {
                        string path = containerAndPath.Substring(pos + 1);

                        return blobContainer.GetBlockBlobReference(path);
                    }
                }
            }

            return null;
        }
    }
}
