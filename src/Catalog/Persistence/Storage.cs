// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class Storage : IStorage
    {
        public Uri BaseAddress { get; protected set; }
        public bool Verbose { get; protected set; }

        public Storage(Uri baseAddress)
        {
            UriBuilder uriBuilder = new UriBuilder(baseAddress.AbsoluteUri);
            // Remove the query string from the base address.
            uriBuilder.Query = string.Empty;
            string s = uriBuilder.Uri.OriginalString.TrimEnd('/') + '/';
            BaseAddress = new Uri(s);
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
        protected abstract Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken);
        protected abstract Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken);
        protected abstract Task OnDeleteAsync(Uri resourceUri, DeleteRequestOptions deleteRequestOptions, CancellationToken cancellationToken);

        public virtual Task<OptimisticConcurrencyControlToken> GetOptimisticConcurrencyControlTokenAsync(
            Uri resourceUri,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

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

        public async virtual Task SaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            TraceMethod(nameof(SaveAsync), resourceUri);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await OnSaveAsync(resourceUri, content, cancellationToken);
            }
            catch (Exception e)
            {
                TraceException(nameof(SaveAsync), resourceUri, e);
                throw;
            }

            TraceExecutionTime(nameof(SaveAsync), resourceUri, stopwatch.ElapsedMilliseconds);
        }

        public async Task<StorageContent> LoadAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            StorageContent storageContent = null;

            TraceMethod(nameof(LoadAsync), resourceUri);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                storageContent = await OnLoadAsync(resourceUri, cancellationToken);
            }
            catch (Exception e)
            {
                TraceException(nameof(LoadAsync), resourceUri, e);
                throw;
            }

            TraceExecutionTime(nameof(LoadAsync), resourceUri, stopwatch.ElapsedMilliseconds);

            return storageContent;
        }

        public async Task DeleteAsync(Uri resourceUri, CancellationToken cancellationToken, DeleteRequestOptions deleteRequestOptions = null)
        {
            TraceMethod(nameof(DeleteAsync), resourceUri);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await OnDeleteAsync(resourceUri, deleteRequestOptions, cancellationToken);
            }
            catch (CloudBlobStorageException e)
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
                TraceException(nameof(DeleteAsync), resourceUri, e);
                throw;
            }

            TraceExecutionTime(nameof(DeleteAsync), resourceUri, stopwatch.ElapsedMilliseconds);
        }

        public async Task<string> LoadStringAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            StorageContent content = await LoadAsync(resourceUri, cancellationToken);
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

        public abstract Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken);

        public virtual Task<bool> UpdateCacheControlAsync(Uri resourceUri, string cacheControl, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public abstract bool Exists(string fileName);

        public Uri ResolveUri(string relativeUri)
        {
            return new Uri(BaseAddress, relativeUri);
        }

        /// <summary>
        /// It will return false if there are changes between the source and destination.
        /// </summary>
        /// <param name="firstResourceUri">The first uri.</param>
        /// <param name="secondResourceUri">The second uri.</param>
        /// <returns>Default returns false.</returns>
        public virtual Task<bool> AreSynchronized(Uri firstResourceUri, Uri secondResourceUri)
        {
            return Task.FromResult(false);
        }

        protected string GetName(Uri uri)
        {
            var address = Uri.UnescapeDataString(BaseAddress.GetLeftPart(UriPartial.Path));
            if (!address.EndsWith("/"))
            {
                address += "/";
            }
            var uriString = uri.ToString();

            int baseAddressLength = address.Length;

            var name = uriString.Substring(baseAddressLength);
            if (name.Contains("#"))
            {
                name = name.Substring(0, name.IndexOf("#"));
            }
            return name;
        }

        public virtual Uri GetUri(string name)
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
                //The Uri depends on the storage implementation.
                Uri storageUri = GetUri(GetName(resourceUri));
                Trace.WriteLine(String.Format("{0} {1}", method, storageUri));
            }
        }

        private string TraceException(string method, Uri resourceUri, Exception exception)
        {
            string message = $"{method} EXCEPTION: {GetUri(GetName(resourceUri))} {exception.ToString()}";
            Trace.WriteLine(message);
            return message;
        }

        private void TraceExecutionTime(string method, Uri resourceUri, long executionTimeInMilliseconds)
        {
            string message = JsonConvert.SerializeObject(new { MethodName = method, StreamUri = GetUri(GetName(resourceUri)), ExecutionTimeInMilliseconds = executionTimeInMilliseconds });
            Trace.WriteLine(message);
        }
    }
}