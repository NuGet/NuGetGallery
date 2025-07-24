// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private const string SecondaryHostPostfix = "-secondary";
        private readonly string _storageConnectionString;
        private readonly bool _readAccessGeoRedundant = false;
        private readonly TimeSpan? _requestTimeout = null;
        private readonly Lazy<Uri> _primaryServiceUri;
        private readonly Lazy<Uri> _secondaryServiceUri;
        private readonly Lazy<BlobServiceClient> _blobClient;
        private readonly TokenCredential _tokenCredential = null;

        public CloudBlobClientWrapper(string storageConnectionString, bool readAccessGeoRedundant = false, TimeSpan? requestTimeout = null)
            : this(storageConnectionString)
        {
            _readAccessGeoRedundant = readAccessGeoRedundant;
            _requestTimeout = requestTimeout; // OK to be null
        }

        private CloudBlobClientWrapper(
            string storageConnectionString,
            TokenCredential tokenCredential,
            bool readAccessGeoRedundant = false,
            TimeSpan? requestTimeout = null)
            : this(storageConnectionString, readAccessGeoRedundant, requestTimeout)
        {
            _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
        }

        private CloudBlobClientWrapper(string storageConnectionString)
        {
            // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
            _storageConnectionString = storageConnectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");
            _primaryServiceUri = new Lazy<Uri>(GetPrimaryServiceUri);
            _secondaryServiceUri = new Lazy<Uri>(GetSecondaryServiceUri);
            _blobClient = new Lazy<BlobServiceClient>(CreateBlobServiceClient);
        }

        public static CloudBlobClientWrapper UsingMsi(
            string storageConnectionString,
            string clientId = null,
            bool readAccessGeoRedundant = false,
            TimeSpan? requestTimeout = null)
        {
            var tokenCredential = new ManagedIdentityCredential(clientId);
            return new CloudBlobClientWrapper(storageConnectionString, tokenCredential, readAccessGeoRedundant, requestTimeout);
        }

        public static CloudBlobClientWrapper UsingServicePrincipal(
            string storageConnectionString, 
            string appID, 
            string subjectAlternativeName, 
            string tenantId, 
            string authorityHost,
            bool readAccessGeoRedundant = false,
            TimeSpan? requestTimeout = null) 
        {
            var tokenCredential = GetCredentialUsingServicePrincipal(appID, subjectAlternativeName, tenantId, authorityHost);
            return new CloudBlobClientWrapper(storageConnectionString, tokenCredential, readAccessGeoRedundant, requestTimeout);
        }

        public static CloudBlobClientWrapper UsingDefaultAzureCredential(
            string storageConnectionString,
            string clientId = null,
            bool readAccessGeoRedundant = false,
            TimeSpan? requestTimeout = null)
        {
#if DEBUG
            var tokenCredential = new DefaultAzureCredential();
            return new CloudBlobClientWrapper(storageConnectionString, tokenCredential, readAccessGeoRedundant, requestTimeout);
#else
            throw new InvalidOperationException("DefaultAzureCredential is only supported in DEBUG builds.");
#endif
        }

        public ISimpleCloudBlob GetBlobFromUri(Uri uri)
        {
            // For Azure blobs, the query string is assumed to be the SAS token.
            ISimpleCloudBlob blob;
            if (!string.IsNullOrEmpty(uri.Query))
            {
                var uriBuilder = new UriBuilder(uri);
                uriBuilder.Query = string.Empty;

                blob = new CloudBlobWrapper(new BlockBlobClient(
                    uriBuilder.Uri,
                    new AzureSasCredential(uri.Query)), uri);
            }
            else
            {
                blob = new CloudBlobWrapper(new BlockBlobClient(uri), container: null);
            }

            return blob;
        }

        public ICloudBlobContainer GetContainerReference(string containerAddress)
        {
            return new CloudBlobContainerWrapper(_blobClient.Value.GetBlobContainerClient(containerAddress), this);
        }

        internal BlockBlobClient CreateBlockBlobClient(CloudBlobWrapper original, BlobClientOptions newOptions)
        {
            if (_readAccessGeoRedundant)
            {
                newOptions.GeoRedundantSecondaryUri = _secondaryServiceUri.Value;
            }
            if (_tokenCredential != null)
            {
                return new BlockBlobClient(original.Uri, _tokenCredential, newOptions);
            }
            return new BlockBlobClient(_storageConnectionString, original.Container, original.Name, newOptions);
        }

        internal BlobContainerClient CreateBlobContainerClient(string containerName, TimeSpan requestTimeout)
        {
            if (containerName == null)
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            var newService = CreateBlobServiceClient(CreateBlobOptions(_readAccessGeoRedundant, requestTimeout));
            return newService.GetBlobContainerClient(containerName);
        }

        internal BlobContainerClient CreateBlobContainerClient(CloudBlobLocationMode locationMode, string containerName, TimeSpan? requestTimeout = null)
        {
            if (containerName == null)
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            if ((locationMode == CloudBlobLocationMode.PrimaryThenSecondary)
                || (!_readAccessGeoRedundant && locationMode == CloudBlobLocationMode.PrimaryOnly))
            {
                // Requested location mode is the same as we expect.
                // If we are not supposed to be using RA-GRS, then there is no difference between PrimaryOnly and PrimaryThenSecondary
                if (requestTimeout.HasValue)
                {
                    return CreateBlobContainerClient(containerName, requestTimeout.Value);
                }
                return null;
            }

            if (locationMode == CloudBlobLocationMode.SecondaryOnly)
            {
                if (!_readAccessGeoRedundant)
                {
                    throw new InvalidOperationException("Can't get secondary region for non RA-GRS storage services");
                }
                var service = CreateSecondaryBlobServiceClient(CreateBlobOptions(readAccessGeoRedundant: false, requestTimeout));
                return service.GetBlobContainerClient(containerName);
            }
            if (locationMode == CloudBlobLocationMode.PrimaryOnly)
            {
                var service = CreateBlobServiceClient(CreateBlobOptions(readAccessGeoRedundant: false, requestTimeout));
                return service.GetBlobContainerClient(containerName);
            }
            throw new ArgumentOutOfRangeException(nameof(locationMode));
        }

        internal BlobServiceClient Client => _blobClient.Value;
        internal bool UsingTokenCredential => _tokenCredential != null;

        private Uri GetPrimaryServiceUri()
        {
            var tempClient = new BlobServiceClient(_storageConnectionString);
            // if _storageConnectionString has SAS token, Uri will contain SAS signature, we need to strip it
            var uriBuilder = new UriBuilder(tempClient.Uri);
            uriBuilder.Query = "";
            uriBuilder.Fragment = "";
            return uriBuilder.Uri;
        }

        private Uri GetSecondaryServiceUri()
        {
            var uriBuilder = new UriBuilder(_primaryServiceUri.Value);
            var hostParts = uriBuilder.Host.Split('.');
            hostParts[0] = hostParts[0] + SecondaryHostPostfix;
            uriBuilder.Host = string.Join(".", hostParts);
            return uriBuilder.Uri;
        }

        private BlobServiceClient CreateBlobServiceClient()
        {
            return CreateBlobServiceClient(CreateBlobOptions(_readAccessGeoRedundant));
        }

        private BlobClientOptions CreateBlobOptions(bool readAccessGeoRedundant, TimeSpan? requestTimeout = null)
        {
            var options = new BlobClientOptions();
            if (readAccessGeoRedundant)
            {
                options.GeoRedundantSecondaryUri = _secondaryServiceUri.Value;
            }
            if (requestTimeout.HasValue)
            {
                options.Retry.NetworkTimeout = requestTimeout.Value;
            }
            else if (_requestTimeout.HasValue)
            {
                options.Retry.NetworkTimeout = _requestTimeout.Value;
            }

            return options;
        }

        private BlobServiceClient CreateBlobServiceClient(BlobClientOptions options)
        {
            if (_tokenCredential != null)
            {
                return new BlobServiceClient(_primaryServiceUri.Value, _tokenCredential, options);
            }
            return new BlobServiceClient(_storageConnectionString, options);
        }

        private BlobServiceClient CreateSecondaryBlobServiceClient(BlobClientOptions options)
        {
            if (_tokenCredential != null)
            {
                return new BlobServiceClient(_secondaryServiceUri.Value, _tokenCredential, options);
            }
            string secondaryConnectionString = GetSecondaryConnectionString();
            return new BlobServiceClient(secondaryConnectionString, options);
        }

        private string GetSecondaryConnectionString()
        {
            var primaryAccountName = _primaryServiceUri.Value.Host.Split('.')[0];
            var secondaryAccountName = _secondaryServiceUri.Value.Host.Split('.')[0];
            var secondaryConnectionString = _storageConnectionString
                .Replace($"https://{primaryAccountName}.", $"https://{secondaryAccountName}.")
                .Replace($"AccountName={primaryAccountName};", $"AccountName={secondaryAccountName};");
            return secondaryConnectionString;
        }

        /// <summary>
        /// Gets credential using the Service Principal.  If the resource is in a different tenant, this is how to access it.
        /// The ServicePrincipal needs to be a "Storage Table/Blob/Queue Data Contributor" role on the storage account.  Owner isn't enough.
        /// </summary>
        /// <returns>ClientCertificatCredential to be used to communicate with Storage.</returns>
        private static ClientCertificateCredential GetCredentialUsingServicePrincipal(string appID, string subjectAlternativeName, string tenantId, string authorityHost)
        {
            X509Certificate2 clientCert;

            // Azure.Identity library doesn't support referencing cert by Store + Subject name, so we need to load it ourselves.
            using (X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);

                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindBySubjectName, subjectAlternativeName, true);

                if (certs.Count == 0)
                {
                    throw new InvalidOperationException($"Unable to find certificate with subject name '{subjectAlternativeName}'");
                }

                // As an exception to comment in GetKeyVaultCertsAsync method, this X509Certificate2 object does not have to be disposed
                // because it is referencing a platform certificate from CurrentUser certificate store, so no temporary files are created for this object.
                clientCert = certs.Cast<X509Certificate2>()
                                .Where(c => c.NotBefore < DateTime.UtcNow && c.NotAfter > DateTime.UtcNow)
                                .OrderBy(x => x.NotAfter).Last();
            }

            return new ClientCertificateCredential(tenantId, appID, clientCert, new ClientCertificateCredentialOptions { AuthorityHost = new Uri(authorityHost), SendCertificateChain = true });
        }
    }
}
