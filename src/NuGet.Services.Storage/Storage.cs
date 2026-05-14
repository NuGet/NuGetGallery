// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Storage
{
    public abstract class Storage : IStorage
    {
        private readonly ILogger<Storage> _logger;

        public Storage(Uri baseAddress, ILogger<Storage> logger)
        {
            string s = baseAddress.OriginalString.TrimEnd('/') + '/';
            BaseAddress = new Uri(s);
            _logger = logger;
        }

        public override string ToString()
        {
            return BaseAddress.ToString();
        }

        protected abstract Task OnCopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellationToken);
        protected abstract Task OnSave(Uri resourceUri, StorageContent content, bool overwrite, CancellationToken cancellationToken);
        protected abstract Task<StorageContent> OnLoad(Uri resourceUri, CancellationToken cancellationToken);
        protected abstract Task OnDelete(Uri resourceUri, CancellationToken cancellationToken);

        public async Task CopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellationToken)
        {
            TraceMethod(nameof(CopyAsync), sourceUri);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await OnCopyAsync(sourceUri, destinationStorage, destinationUri, destinationProperties, cancellationToken);
            }
            catch (Exception e)
            {
                TraceException(nameof(CopyAsync), sourceUri, e);
                throw;
            }

            TraceExecutionTime(nameof(CopyAsync), sourceUri, stopwatch.ElapsedMilliseconds);
        }


        public async Task Save(Uri resourceUri, StorageContent content, bool overwrite, CancellationToken cancellationToken)
        {
            SaveCount++;

            TraceMethod("SAVE", resourceUri);

            try
            {
                await OnSave(resourceUri, content, overwrite, cancellationToken);
            }
            catch (Exception e)
            {
                string message = String.Format("SAVE EXCEPTION: {0} {1}", resourceUri, e.Message);
                _logger.LogError("SAVE EXCEPTION: {ResourceUri} {Exception}", resourceUri, e);
                throw new Exception(message, e);
            }
        }

        public async Task<StorageContent> Load(Uri resourceUri, CancellationToken cancellationToken)
        {
            LoadCount++;

            TraceMethod("LOAD", resourceUri);

            try
            {
                return await OnLoad(resourceUri, cancellationToken);
            }
            catch (Exception e)
            {
                string message = String.Format("LOAD EXCEPTION: {0} {1}", resourceUri, e.Message);
                _logger.LogError("LOAD EXCEPTION: {ResourceUri} {Exception}", resourceUri, e);
                throw new Exception(message, e);
            }
        }

        public async Task Delete(Uri resourceUri, CancellationToken cancellationToken)
        {
            DeleteCount++;

            TraceMethod("DELETE", resourceUri);

            try
            {
                await OnDelete(resourceUri, cancellationToken);
            }
            catch (RequestFailedException e)
            {
                WebException webException = e.InnerException as WebException;
                if (webException != null)
                {
                    HttpStatusCode statusCode = ((HttpWebResponse)webException.Response).StatusCode;
                    if (statusCode != HttpStatusCode.NotFound) 
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
            catch (Exception e)
            {
                string message = String.Format("DELETE EXCEPTION: {0} {1}", resourceUri, e.Message);
                _logger.LogError("DELETE EXCEPTION: {ResourceUri} {Exception}", resourceUri, e);
                throw new Exception(message, e);
            }
        }

        public async Task<string> LoadString(Uri resourceUri, CancellationToken cancellationToken)
        {
            StorageContent content = await Load(resourceUri, cancellationToken);
            if (content == null)
            {
                return null;
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    StreamReader reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }
            }
        }

        public Uri BaseAddress { get; protected set; }
        public abstract bool Exists(string fileName);
        public abstract Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken);
        public abstract IEnumerable<StorageListItem> List(bool getMetadata);
        public abstract Task<IEnumerable<StorageListItem>> ListAsync(bool getMetadata, CancellationToken cancellationToken);
        public abstract Task<IEnumerable<StorageListItem>> ListTopLevelAsync(bool getMetadata, CancellationToken cancellationToken);

        public bool Verbose
        {
            get;
            set;
        }

        public int SaveCount
        {
            get;
            protected set;
        }

        public int LoadCount
        {
            get;
            protected set;
        }

        public int DeleteCount
        {
            get;
            protected set;
        }

        public void ResetStatistics()
        {
            SaveCount = 0;
            LoadCount = 0;
            DeleteCount = 0;
        }

        /// <summary>
        /// Validate that relative URI is a simple, additive path and add it to a base address. Reject non-relative URIs or badly formed relative URIs.
        /// </summary>
        public static Uri ResolveUri(Uri baseAddress, string relativeUri)
        {
            AssertSimpleBlobName(relativeUri);
            return new Uri(baseAddress.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/" + relativeUri.TrimStart('/'));
        }

        public Uri ResolveUri(string relativeUri)
        {
            return ResolveUri(BaseAddress, relativeUri);
        }

        public static string GetName(Uri baseAddress, Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (baseAddress is null)
            {
                throw new ArgumentNullException(nameof(baseAddress));
            }

            if (!uri.IsAbsoluteUri)
            {
                throw new ArgumentException($"'{nameof(uri)}' must be an absolute URI.", nameof(uri));
            }

            if (!baseAddress.IsAbsoluteUri)
            {
                throw new ArgumentException($"'{nameof(baseAddress)}' must be an absolute URI.", nameof(baseAddress));
            }

            if (uri.AbsoluteUri.IndexOf("%2F", StringComparison.OrdinalIgnoreCase) >= 0 // Encoded forward slash
                || uri.AbsoluteUri.IndexOf("%5C", StringComparison.OrdinalIgnoreCase) >= 0) // Encoded backslash
            {
                throw new ArgumentException("The input URI must not contain encoded forward slashes or back slashes.", nameof(uri));
            }

            // The GetLeftPart method performs encoding under the hood, which could be problematic if it contains Unicode characters.
            // It doesn't perform double encoding; the Uri object knows if it's already encoded and skips encoding it again.
            // Decoding the base address to remove any encoded characters.
            var baseAddressStr = Uri.UnescapeDataString(baseAddress.GetLeftPart(UriPartial.Path)); // Remove potential query or SAS from the URI
            if (!baseAddressStr.EndsWith("/"))
            {
                baseAddressStr += "/";
            }

            // Do the same with the above to get it decoded.
            string uriStr = Uri.UnescapeDataString(uri.GetLeftPart(UriPartial.Path)); // Remove potential query or SAS from the URI

            // handle mismatched scheme (http vs https)
            if (uri.Scheme != baseAddress.Scheme)
            {
                uriStr = baseAddress.Scheme + uriStr.Substring(uri.Scheme.Length);
            }

            if (!uriStr.StartsWith(baseAddressStr, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The input {nameof(uri)} '{uri.AbsoluteUri}' must start with the base address '{baseAddress.AbsoluteUri}'.", nameof(uri));
            }

            string name = uriStr.Substring(baseAddressStr.Length);
            name = name.TrimStart('/');

            AssertSimpleBlobName(name);

            return name;
        }

        protected string GetName(Uri uri)
        {
            return GetName(BaseAddress, uri);
        }

        private static void AssertSimpleBlobName(string relativeUri)
        {
            if (relativeUri.IndexOfAny(['?', '#']) >= 0)
            {
                throw new ArgumentException($"{nameof(relativeUri)} '{relativeUri}' must not contain a fragment or query string.");
            }

            // Enforce https://learn.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata#blob-names
            var pieces = relativeUri.TrimStart('/').Split('/');
            foreach (var piece in pieces)
            {
                if (piece.Length == 0)
                {
                    throw new ArgumentException($"{nameof(relativeUri)} '{relativeUri}' must not have an empty path segment.");
                }

                var decoded = Uri.UnescapeDataString(piece);
                if (decoded.StartsWith(".") || decoded.EndsWith("."))
                {
                    throw new ArgumentException($"{nameof(relativeUri)} '{relativeUri}' must not have a path segment ending in a dot.");
                }

                if (decoded.Contains("\\") || decoded.Contains("/"))
                {
                    throw new ArgumentException($"{nameof(relativeUri)} '{relativeUri}' must not have a path segment containing a forward slash or backslash.");
                }
            }
        }

        protected Uri GetUri(string name)
        {
            string address = BaseAddress.ToString();
            if (!address.EndsWith("/"))
            {
                address += "/";
            }
            address += name.Replace("\\", "/").TrimStart('/');

            return new Uri(address);
        }

        protected void TraceMethod(string method, Uri resourceUri)
        {
            if (Verbose)
            {
                _logger.LogInformation("{Method} {ResourceUri}", method, resourceUri);
            }
        }

        private void TraceException(string method, Uri resourceUri, Exception exception)
        {
            _logger.LogError(exception, "{Method} threw exception for {Url}", method, resourceUri);
        }

        private void TraceExecutionTime(string method, Uri resourceUri, long executionTimeInMilliseconds)
        {
            _logger.LogInformation("Execution time: {@data}", new
            {
                MethodName = method,
                StreamUri = GetUri(GetName(resourceUri)),
                ExecutionTimeInMilliseconds = executionTimeInMilliseconds
            });
        }

        public abstract Task SetMetadataAsync(Uri resourceUri, IDictionary<string, string> metadata);
    }
}
