// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// User-subscribed security policy.
    /// </summary>
    public class UserSecurityPolicy : IEntity, IEquatable<UserSecurityPolicy>
    {
        public UserSecurityPolicy()
        {
        }

        public UserSecurityPolicy(UserSecurityPolicy policy)
            : this(policy.Name, policy.Subscription, policy.Value)
        {
        }

        public UserSecurityPolicy(string name, string subscription, string value = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Subscription = subscription ?? throw new ArgumentNullException(nameof(subscription));
            Value = value;
        }

        /// <summary>
        /// Policy key.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// User key.
        /// </summary>
        [Index("IX_UserSecurityPolicy_UserKeyNameSubscription", IsUnique = true, Order = 0)]
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
        [Index("IX_UserSecurityPolicy_UserKeyNameSubscription", IsUnique = true, Order = 1)]
        public string Name { get; set; }

        /// <summary>
        /// Name of subscription that added this policy.
        /// </summary>
        [Required]
        [StringLength(256)]
        [Index("IX_UserSecurityPolicy_UserKeyNameSubscription", IsUnique = true, Order = 2)]
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

        private static readonly Func<object, long, long> _hash = (i, hash) => ((hash << 5) + hash) ^ (i?.GetHashCode() ?? 0);
        private const long _seed = 0x1505L;

        public override int GetHashCode()
        {
            return _hash(Value, _hash(Subscription, _hash(Name, _seed))).GetHashCode();
        }
    }
}