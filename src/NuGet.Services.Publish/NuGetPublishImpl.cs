// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Services.Publish
{
    public static class NuGetPublishImpl
    {
        private static readonly ConfigurationService _configurationService;

        static NuGetPublishImpl()
        {
            _configurationService = new ConfigurationService();
        }

        public static async Task Upload(IOwinContext context, CancellationToken cancellationToken)
        {
            string name = await ValidateRequest(context);

            if (name != null)
            {
                Stream nupkgStream = context.Request.Body;

                Uri nupkgAddress = await SaveNupkg(nupkgStream, name);
                Uri catalogAddress = await AddToCatalog(nupkgStream, cancellationToken);

                JToken response = new JObject
                { 
                    { "download", nupkgAddress.ToString() }, 
                    { "catalog", catalogAddress.ToString() }
                };

                await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
            }
        }

        static async Task<string> ValidateRequest(IOwinContext context)
        {
            Stream nupkgStream = context.Request.Body;

            using (ZipArchive archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, true))
            {
                XDocument original = Utils.GetNuspec(archive);
                XDocument nuspec = Utils.NormalizeNuspecNamespace(original);

                XNamespace ns = XNamespace.Get("http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd");

                XElement metadata = nuspec.Element(ns.GetName("package")).Element(ns.GetName("metadata"));
                string id = metadata.Element(ns.GetName("id")).Value;
                string version = metadata.Element(ns.GetName("version")).Value;

                if (await ValidateCurrentUserIsOwner(id))
                {
                    //TODO: perform addition validation

                    return MakeNupkgName(id, version);
                }
            }

            return null;
        }

        static async Task<bool> ValidateCurrentUserIsOwner(string id)
        {
            //TODO: check the current user is a member of the registration owners

            return await Task.FromResult(true);
        }

        static async Task<Uri> SaveNupkg(Stream nupkgStream, string name)
        {
            string storagePrimary = _configurationService.Get("Storage.Primary");
            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);

            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("nupkgs");
            await container.CreateIfNotExistsAsync();

            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = "application/octet-stream";
            blob.Properties.ContentDisposition = name;
            nupkgStream.Seek(0, SeekOrigin.Begin);
            await blob.UploadFromStreamAsync(nupkgStream);

            return blob.Uri;
        }

        static string MakeNupkgName(string id, string version)
        {
            return string.Format("{0}.{1}.nupkg", id, version).ToLowerInvariant();
        }

        static async Task<Uri> AddToCatalog(Stream nupkgStream, CancellationToken cancellationToken)
        {
            string storagePrimary = _configurationService.Get("Storage.Primary");
            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);

            Storage storage = new AzureStorage(account, "catalog");

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage);
            writer.Add(Utils.CreateCatalogItem(nupkgStream, null, null, ""));
            await writer.Commit(null, cancellationToken);

            return writer.RootUri;
        }
    }
}