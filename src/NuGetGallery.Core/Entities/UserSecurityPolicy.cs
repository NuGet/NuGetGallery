// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// User-subscribed security policy.
    /// </summary>
    public class UserSecurityPolicy : IEntity
    {
        public UserSecurityPolicy()
        {
        }

        public UserSecurityPolicy(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Policy key.
        /// </summary>
        public int Key { get; set; }
        
        /// <summary>
        /// User key.
        /// </summary>
        public int UserKey { get; set; }

        /// <summary>
        /// User subscribed to this security policy.
        /// </summary>
        public User User { get; set; }

        /// <summary>
        /// Type name for the policy handler that provides policy behavior.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// Support for JSON-serialized properties for specific policies.
        /// </summary>
        public string Value { get; set; }
    }
}