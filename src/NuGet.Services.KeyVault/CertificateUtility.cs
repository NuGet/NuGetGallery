// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Services.KeyVault
{
    public static class CertificateUtility
    {
        public static X509Certificate2 FindCertificateByThumbprint(StoreName storeName, StoreLocation storeLocation, string thumbprint, bool validationRequired)
        {
            var store = new X509Store(storeName, storeLocation);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var col = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validationRequired);
                if (col.Count == 0)
                {
                    throw new ArgumentException(
                        $"Certificate with thumbprint {thumbprint} and validation {(validationRequired ? "required" : "not required")} was not found in store {storeLocation} {storeName}.");
                }

                return col[0];
            }
            finally
            {
                store.Close();
            }
        }
    }
}
