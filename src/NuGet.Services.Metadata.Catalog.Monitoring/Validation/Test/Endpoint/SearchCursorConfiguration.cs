// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public enum SearchCursorCredentialType
    {
        Anonymous = 1,
        AzureSasCredential,
        DefaultAzureCredential,
        ManagedIdentityCredential,
    }

    public sealed class SearchCursorConfiguration
    {
        public SearchCursorConfiguration(Uri cursorUri, BlobClient blobClient, SearchCursorCredentialType credentialType)
        {
            CursorUri = cursorUri;
            BlobClient = blobClient;
            CredentialType = credentialType;
        }

        public Uri CursorUri { get; }
        public BlobClient BlobClient { get; }
        public SearchCursorCredentialType CredentialType { get; }
    }
}
