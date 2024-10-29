// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public sealed class ListCertificateItemViewModel
    {
        /// <summary>
        /// This value allows an abbreviated issuer and subject to show up on a single line in the web UI in the
        /// "MD" Bootstrap screen size. This is approximately the P75 of CN length on NuGet.org.
        /// </summary>
        private const int AbbreviationLength = 36;

        public string Thumbprint { get; }
        public bool HasInfo { get; }
        public bool IsExpired { get; }
        public string ExpirationDisplay { get; }
        public string ExpirationIso { get; }
        public string Subject { get; }
        public string Issuer { get; }
        public string ShortSubject { get; }
        public string ShortIssuer { get; }
        public bool CanDelete { get; }
        public string DeleteUrl { get; }

        public ListCertificateItemViewModel(Certificate certificate, string deleteUrl)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            Thumbprint = certificate.Thumbprint;
            HasInfo = certificate.Expiration.HasValue;
            if (certificate.Expiration.HasValue)
            {
                // The value stored in the database is assumed to be UTC.
                var expirationUtc = DateTime.SpecifyKind(certificate.Expiration.Value, DateTimeKind.Utc);
                IsExpired = expirationUtc < DateTime.UtcNow;
                ExpirationDisplay = expirationUtc.ToNuGetShortDateString();
                ExpirationIso = expirationUtc.ToString("O");
            }
            Subject = certificate.Subject;
            Issuer = certificate.Issuer;
            ShortSubject = certificate.ShortSubject ?? certificate.Subject?.Abbreviate(AbbreviationLength);
            ShortIssuer = certificate.ShortIssuer ?? certificate.Issuer?.Abbreviate(AbbreviationLength);
            CanDelete = !string.IsNullOrEmpty(deleteUrl);
            DeleteUrl = deleteUrl;
        }
    }
}