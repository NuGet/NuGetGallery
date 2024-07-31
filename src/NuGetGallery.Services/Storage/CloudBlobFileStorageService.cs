// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;
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

        public async Task<ActionResult> CreateDownloadFileActionResultAsync(Uri requestUrl, string folderName, string fileName, string versionParameter)
        {
            ICloudBlobContainer container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            var redirectUri = GetRedirectUri(requestUrl, blob.Uri, versionParameter);
            return new RedirectResult(redirectUri.AbsoluteUri, false);
        }

        internal async Task<ActionResult> CreateDownloadFileActionResult(
            HttpContextBase httpContext,
            string folderName,
            string fileName,
            string versionParameter)
        {
            var container = await GetContainerAsync(folderName);
            var blob = container.GetBlobReference(fileName);

            var redirectUri = GetRedirectUri(httpContext.Request.Url, blob.Uri, versionParameter);
            return new RedirectResult(redirectUri.AbsoluteUri, false);
        }

        internal Uri GetRedirectUri(Uri requestUrl, Uri blobUri, string versionParameter)
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
            var queryStringUri = !string.IsNullOrEmpty(blobUri.Query)
                ? blobUri
                : requestUrl;

            NameValueCollection queryValues = ParseQueryString(queryStringUri);
            queryValues.Add(CoreConstants.PackageVersionParameterName, versionParameter);

            var urlBuilder = new UriBuilder(scheme, host, port)
            {
                Path = blobUri.LocalPath,
                Query = queryValues.ToString()
            };

            return urlBuilder.Uri;
        }

        public async Task<bool> IsAvailableAsync()
        {
            var container = await GetContainerAsync(CoreConstants.Folders.PackagesFolderName);
            return await container.ExistsAsync(cloudBlobLocationMode: null);
        }

        private static NameValueCollection ParseQueryString(Uri uri)
        {
            if (string.IsNullOrEmpty(uri.Query)) return HttpUtility.ParseQueryString(string.Empty);

            string query = uri.Query.Substring(1);

            NameValueCollection nvcol = HttpUtility.ParseQueryString(query);
            return nvcol;
        }
    }
}
