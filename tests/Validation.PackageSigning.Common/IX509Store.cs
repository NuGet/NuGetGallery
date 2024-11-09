// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public interface IX509Store
    {
        void Add(StoreLocation storeLocation, StoreName storeName, X509Certificate2 certificate);
        void Remove(StoreLocation storeLocation, StoreName storeName, X509Certificate2 certificate);
    }
}
