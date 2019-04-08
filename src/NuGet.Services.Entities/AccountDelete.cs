// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class AccountDelete
        : IEntity
    {
        public int Key { get; set; }

        /// <summary>
        /// The date when the account was deleted.
        /// </summary>
        [Required]
        public DateTime DeletedOn { get; set; }

        /// <summary>
        /// The username that the account deleted had.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The <see cref="User"/> (admin) key that deleted the account.
        /// </summary>
        public int? DeletedByKey { get; set; }

        /// <summary>
        /// The <see cref="User"/> (admin) that deleted the account
        /// </summary>
        public User DeletedBy { get; set; }

         /// <summary>
         /// The signature of the admin.
         /// </summary>
         public string Signature { get; set; }
    }
}