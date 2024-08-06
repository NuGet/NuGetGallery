﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        public Uri ResolveUri(string relativeUri)
        {
            return new Uri(BaseAddress, relativeUri);
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
