// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Text;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class OwnershipRegistration
    {
        public string Id { get; set; }
        public string Namespace { get; set; }

        public Uri GetUri(Uri baseAddress)
        {
            string ns = string.IsNullOrEmpty(Namespace) ? "nuget.org" : Namespace;
            string fragment = string.Format("#registration/{0}/{1}", ns, Id);
            return new Uri(baseAddress, fragment);
        }

        public string GetKey()
        {
            string ns = string.IsNullOrEmpty(Namespace) ? "nuget.org" : Namespace;
            string key = string.Format("{0}/{1}", ns, Id);
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            string base64 = Convert.ToBase64String(bytes);
            return base64;
        }
    }
}
