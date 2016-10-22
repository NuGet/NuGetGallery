﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGetGallery
{
    public class Credential
        : IEntity
    {
        /// <summary>
        /// Represents a credential used by NuGet Gallery. Can be an API key credential,
        /// username/password or external credential like Microsoft Account or Azure Active Directory.
        /// </summary>
        public Credential()
        {
        }


        /// <summary>
        /// Represents a credential used by NuGet Gallery. Can be an API key credential,
        /// username/password or external credential like Microsoft Account or Azure Active Directory.
        /// </summary>
        /// <param name="type">Credential type. See <see cref="CredentialTypes"/></param>
        /// <param name="value">Credential value</param>
        public Credential(string type, string value)
            : this()
        {
            Type = type;
            Value = value;
        }

        /// <summary>
        /// Represents a credential used by NuGet Gallery. Can be an API key credential,
        /// username/password or external credential like Microsoft Account or Azure Active Directory.
        /// </summary>
        /// <param name="type">Credential type. See <see cref="CredentialTypes"/></param>
        /// <param name="value">Credential value</param>
        /// <param name="expiration">Optional expiration timespan for the credential.</param>
        public Credential(string type, string value, TimeSpan? expiration)
            : this(type, value)
        {
            if (expiration.HasValue && expiration.Value > TimeSpan.Zero)
            {
                Expires = DateTime.UtcNow.Add(expiration.Value);
            }
        }

        public int Key { get; set; }

        [Required]
        public int UserKey { get; set; }

        [Required]
        [StringLength(maximumLength: 64)]
        public string Type { get; set; }

        [Required]
        [StringLength(maximumLength: 256)]
        public string Value { get; set; }

        [StringLength(maximumLength: 256)]
        public string Identity { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime Created { get; set; }

        public DateTime? Expires { get; set; }

        public DateTime? LastUsed { get; set; }

        public virtual User User { get; set; }

        [NotMapped]
        public bool HasExpired
        {
            get
            {
                if (Expires.HasValue)
                {
                    return DateTime.UtcNow >= Expires.Value;
                }

                return false;
            }
        }

        public bool HasBeenUsedInLastDays(int numberOfDays)
        {
            if (numberOfDays > 0 && LastUsed.HasValue)
            {
                return LastUsed.Value.AddDays(numberOfDays) > DateTime.UtcNow;
            }

            return true;
        }
    }
}
