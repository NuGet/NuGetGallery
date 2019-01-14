// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents a relationship between a user and certificate.
    /// </summary>
    public class UserCertificate : IEntity
    {
        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the foreign key of the certificate entity.
        /// </summary>
        [Index("IX_UserCertificates_CertificateKeyUserKey", IsUnique = true, Order = 0)]
        public int CertificateKey { get; set; }

        /// <summary>
        /// Gets or sets the certificate entity.
        /// </summary>
        public virtual Certificate Certificate { get; set; }

        /// <summary>
        /// Gets or sets the foreign key of the user entity.
        /// </summary>
        [Index("IX_UserCertificates_CertificateKeyUserKey", IsUnique = true, Order = 1)]
        public int UserKey { get; set; }

        /// <summary>
        /// Gets or sets the user entity.
        /// </summary>
        public virtual User User { get; set; }
    }
}