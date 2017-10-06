// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class Storage : IStorage
    {
        public Storage(Uri baseAddress)
        {
            string s = baseAddress.OriginalString.TrimEnd('/') + '/';
            BaseAddress = new Uri(s);
        }

        public override string ToString()
        {
            return BaseAddress.ToString();
        }

        protected abstract Task OnSave(Uri resourceUri, StorageContent content, CancellationToken cancellationToken);
        protected abstract Task<StorageContent> OnLoad(Uri resourceUri, CancellationToken cancellationToken);
        protected abstract Task OnDelete(Uri resourceUri, CancellationToken cancellationToken);

        public async Task Save(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            SaveCount++;

            TraceMethod(nameof(Save), resourceUri);
            Stopwatch sw = new Stopwatch();
            sw.Start();

            try
            {
                await OnSave(resourceUri, content, cancellationToken);
            }
            catch (Exception e)
            {
                TraceException(nameof(Save), resourceUri, e);
                throw;
            }

            sw.Stop();
            TraceExecutionTime(nameof(Save), resourceUri, sw.ElapsedMilliseconds);
        }

        public async Task<StorageContent> Load(Uri resourceUri, CancellationToken cancellationToken)
        {
            LoadCount++;
            StorageContent storageContent = null;

            TraceMethod(nameof(Load), resourceUri);
            Stopwatch sw = new Stopwatch();
            sw.Start();

            try
            {
                storageContent = await OnLoad(resourceUri, cancellationToken);
            }
            catch (Exception e)
            {
                TraceException(nameof(Load), resourceUri, e);
                throw;
            }

            sw.Stop();
            TraceExecutionTime(nameof(Load), resourceUri, sw.ElapsedMilliseconds);
            return storageContent;
        }

        public async Task Delete(Uri resourceUri, CancellationToken cancellationToken)
        {
            DeleteCount++;

            TraceMethod(nameof(Delete), resourceUri);
            Stopwatch sw = new Stopwatch();
            sw.Start();

            try
            {
                await OnDelete(resourceUri, cancellationToken);
            }
            catch (StorageException e)
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
                TraceException(nameof(Delete), resourceUri, e);
                throw;
            }

            sw.Stop();
            TraceExecutionTime(nameof(Delete), resourceUri, sw.ElapsedMilliseconds);
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
        public abstract Task<IEnumerable<StorageListItem>> List(CancellationToken cancellationToken);

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
