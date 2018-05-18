// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public sealed class ListCertificateItemViewModel
    {
        public string Sha1Thumbprint { get; }
        public bool CanDelete { get; }
        public string DeleteUrl { get; }

        public ListCertificateItemViewModel(Certificate certificate, string deleteUrl)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            Sha1Thumbprint = certificate.Sha1Thumbprint;
            CanDelete = !string.IsNullOrEmpty(deleteUrl);
            DeleteUrl = deleteUrl;
        }
    }
}