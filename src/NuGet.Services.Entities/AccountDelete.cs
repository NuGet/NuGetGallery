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
        /// The deleted account key.
        /// </summary>
        public int DeletedAccountKey { get; set; }

        /// <summary>
        /// The deleted account.
        /// </summary>
        public User DeletedAccount { get; set; }

        /// <summary>
        /// The User(admin) key that executed the delete action.
        /// </summary>
        public int? DeletedByKey { get; set; }

        /// <summary>
        /// The User(admin) that executed the delete action.
        /// </summary>
        public User DeletedBy { get; set; }

         /// <summary>
         /// The signature of the admin.
         /// </summary>
         public string Signature { get; set; }
    }
}