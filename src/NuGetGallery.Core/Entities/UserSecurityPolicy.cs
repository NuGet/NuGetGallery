// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// User-subscribed security policy.
    /// </summary>
    public class UserSecurityPolicy : IEntity, IEquatable<UserSecurityPolicy>
    {
        public UserSecurityPolicy()
        {
        }

        public UserSecurityPolicy(string name, string subscription, string value = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (string.IsNullOrEmpty(subscription))
            {
                throw new ArgumentNullException(nameof(subscription));
            }

            Name = name;
            Subscription = subscription;
            Value = value;
        }
        
        public UserSecurityPolicy(UserSecurityPolicy policy)
        {
            Name = policy.Name;
            Subscription = policy.Subscription;
            Value = policy.Value;
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
        [StringLength(256)]
        public string Name { get; set; }

        /// <summary>
        /// Name of subscription that added this policy.
        /// </summary>
        [Required]
        [StringLength(256)]
        public string Subscription { get; set; }

        /// <summary>
        /// Support for JSON-serialized properties for specific policies.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Determine if two policies are equal.
        /// </summary>
        public bool Equals(UserSecurityPolicy other)
        {
            return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) &&
                Subscription.Equals(other.Subscription, StringComparison.OrdinalIgnoreCase) &&
                (
                    (string.IsNullOrEmpty(Value) && string.IsNullOrEmpty(other.Value)) ||
                    (Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase))
                );
        }
    }
}