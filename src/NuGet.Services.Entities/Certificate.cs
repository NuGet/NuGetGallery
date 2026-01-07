// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    [DisplayColumn(nameof(Thumbprint))]
    public class Certificate : IEntity
    {
        /// <summary>
        /// The database-mastered identifier for this certificate.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the SHA-1 thumbprint of the certificate.
        /// </summary>
        [Obsolete("This property should not be used since SHA-1 usage is avoided in NuGetGallery.")]
        public string Sha1Thumbprint { get; set; }

        /// <summary>
        /// The SHA-256 thumbprint (fingerprint) that uniquely identifies this certificate. This is a string with
        /// exactly 64 characters and is the hexadecimal encoding of the hash digest. Note that the SQL column that
        /// stores this property allows longer string values to facilitate future hash algorithm changes.
        /// </summary>
        public string Thumbprint { get; set; }

        /// <summary>
        /// The subject distinguished name of the certificate.
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// The short subject of the certificate. In most cases, this is the CN attribute.
        /// </summary>
        public string ShortSubject { get; set; }

        /// <summary>
        /// The issuer distinguished name of the certificate.
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// The short issuer of the certificate. In most cases, this is the CN attribute.
        /// </summary>
        public string ShortIssuer { get; set; }

        /// <summary>
        /// The expiration date and time of the certificate.
        /// </summary>
        public DateTime? Expiration { get; set; }

        /// <summary>
        /// Gets or sets the collection of user certificates.
        /// </summary>
        public ICollection<UserCertificate> UserCertificates { get; set; }

        public Certificate()
        {
            UserCertificates = new List<UserCertificate>();
        }
    }
}