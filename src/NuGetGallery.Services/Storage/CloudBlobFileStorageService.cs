// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public class CloudBlobFileStorageService : CloudBlobCoreFileStorageService, IFileStorageService
    {
        private readonly IAppConfiguration _configuration;
        private readonly ISourceDestinationRedirectPolicy _redirectPolicy;

        public CloudBlobFileStorageService(
            ICloudBlobClient client,
            IAppConfiguration configuration,
            ISourceDestinationRedirectPolicy redirectPolicy,
            IDiagnosticsService diagnosticsService,
            ICloudBlobContainerInformationProvider cloudBlobFolderInformationProvider) 
            : base(client, diagnosticsService, cloudBlobFolderInformationProvider)
        {
            _configuration = configuration;
            _redirectPolicy = redirectPolicy;
        }

        public async Task<ActionResult> CreateDownloadFileActionResultAsync(Uri requestUrl, string folderName, string fileName)
        {
            ICloudBlobContainer container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            var redirectUri = GetRedirectUri(requestUrl, blob.Uri);
            return new RedirectResult(redirectUri.AbsoluteUri, false);
        }

        internal async Task<ActionResult> CreateDownloadFileActionResult(
            HttpContextBase httpContext,
            string folderName,
            string fileName)
        {
            var container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            var redirectUri = GetRedirectUri(httpContext.Request.Url, blob.Uri);
            return new RedirectResult(redirectUri.AbsoluteUri, false);
        }

        internal Uri GetRedirectUri(Uri requestUrl, Uri blobUri)
        {
            if (!_redirectPolicy.IsAllowed(requestUrl, blobUri))
            {
                Trace.TraceInformation("Redirect from {0} to {1} was not allowed", requestUrl, blobUri);
                throw new InvalidOperationException("Unsafe redirects are not allowed");
            }

            string host;
            int port;
            string scheme;
            if (string.IsNullOrEmpty(_configuration.AzureCdnHost))
            {
                host = blobUri.Host;
                port = blobUri.Port;
                scheme = blobUri.Scheme;
            }
            else
            {
                host = _configuration.AzureCdnHost;
                port = requestUrl.Port;
                scheme = requestUrl.Scheme;
            }

            // When a blob query string is passed, that one always wins.
            // This will only happen on private NuGet gallery instances,
            // not on NuGet.org.
            // When no blob query string is passed, we forward the request
            // URI's query string to the CDN. See https://github.com/NuGet/NuGetGallery/issues/3168
            // and related PR's.
            var queryString = !string.IsNullOrEmpty(blobUri.Query)
                ? blobUri.Query
                : requestUrl.Query;

            if (!string.IsNullOrEmpty(queryString))
            {
                queryString = queryString.TrimStart('?');
            }

            var urlBuilder = new UriBuilder(scheme, host, port)
            {
                Path = blobUri.LocalPath,
                Query = queryString
            };

            return urlBuilder.Uri;
        }

        public async Task<bool> IsAvailableAsync()
        {
            var container = await GetContainerAsync(CoreConstants.Folders.PackagesFolderName);
            return await container.ExistsAsync(cloudBlobLocationMode: null);
        }
    }
}
