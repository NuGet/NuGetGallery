// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public enum CertificatePatternType
    {
        /// <summary>
        /// A certificate issued by Azure Trusted Signing service. Certificates have the following rules, in additional
        /// to default NuGet author signing requirements.
        /// - Must have a root of Microsoft Identity Verification Root Certificate Authority 2020
        /// - Must have the "1.3.6.1.4.1.311.97.1.0" EKU (public trust)
        /// - Must have the user provided "1.3.6.1.4.1.311.97.1.3.1.*" EKU (durable identity pinning)
        /// </summary>
        AzureTrustedSigning = 1,
    }

    public class UserCertificatePattern : IEntity
    {
        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the foreign key of the user entity.
        /// </summary>
        public int UserKey { get; set; }

        /// <summary>
        /// Gets or sets the user entity.
        /// </summary>
        public virtual User User { get; set; }

        /// <summary>
        /// The type of the certificate pattern, used to imply certain rules for a certificate and how to use the <see cref="Identifier"/>.
        /// </summary>
        public CertificatePatternType PatternType { get; set; }

        /// <summary>
        /// The date when the pattern was created on.
        /// </summary>
        [Required]
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// A identifying string that is stable across all certificates described by this pattern. The specific
        /// way in which this string is expressed in a certificate depends on the <see cref="PatternType"/> property
        /// </summary>
        /// <remarks>
        /// Currently this is only an OID but the max size was selected to accommodate large distinguished names
        /// if they are needed in the future. 400 is larger than the largest subject name of any certificate in
        /// NuGet.org package signing and is small enough to not exceed the 900 byte limit for indexes on SQL Server
        /// (old versions). The nvarchar type is 2 bytes per character leaving 100 bytes for other columns to
        /// participate in a non-clustered index.
        /// </remarks>
        [StringLength(400)]
        [Required]
        public string Identifier { get; set; }
    }
}