// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess;
using NuGet.Common;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    internal sealed class PlatformX509Store : IX509Store, IRootX509Store
    {
        internal static PlatformX509Store Instance { get; } = new();

        public void Add(StoreLocation storeLocation, StoreName storeName, X509Certificate2 certificate)
        {
            AddCertificateToStore(certificate, storeLocation, storeName);
        }

        public void Remove(StoreLocation storeLocation, StoreName storeName, X509Certificate2 certificate)
        {
            using (X509Store store = new(storeName, storeLocation))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Remove(certificate);
            }
        }

        public void Add(StoreLocation storeLocation, X509Certificate2 certificate)
        {
            Add(storeLocation, StoreName.Root, certificate);
        }

        public void Remove(StoreLocation storeLocation, X509Certificate2 certificate)
        {
            Remove(storeLocation, StoreName.Root, certificate);
        }

        private static void AddCertificateToStore(X509Certificate2 certificate, StoreLocation storeLocation, StoreName storeName)
        {
            using (X509Store store = new(storeName, storeLocation))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
            }
        }
    }
}
