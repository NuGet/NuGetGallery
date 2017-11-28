// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public class CertificateStore : ICertificateStore
    {
        public Task<bool> Exists(string thumbprint)
        {
            throw new NotImplementedException();
        }

        public Task<X509Certificate2> Load(string thumbprint)
        {
            // TODO: verify the certificate's thumbprint each time the certificate is downloaded from blob storage

            throw new NotImplementedException();
        }

        public Task Save(X509Certificate2 certificate)
        {
            throw new NotImplementedException();
        }
    }
}