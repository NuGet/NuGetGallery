// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace NuGet.Services.Entities
{
    public class Scope
        : IEntity
    {
        [JsonIgnore]
        public int Key { get; set; }

        [JsonIgnore]
        public int CredentialKey { get; set; }

        /// <summary>
        /// Package owner (user or organization) scoping.
        /// </summary>
        [JsonProperty("o")]
        public int? OwnerKey { get; set; }

        /// <summary>
        /// Package owner (user or organization) scoping.
        /// </summary>
        [JsonIgnore]
        public virtual User Owner { get; set; }

        /// <summary>
        /// Packages glob pattern.
        /// </summary>
        [JsonProperty("s")]
        public string Subject { get; set; }

        [Required]
        [JsonProperty("a")]
        public string AllowedAction { get; set; }

        [JsonIgnore]
        public virtual Credential Credential { get; set; }

        public Scope()
        {
        }

        public Scope(User owner, string subject, string allowedAction)
        {
            Owner = owner;
            Subject = subject;
            AllowedAction = allowedAction;
        }

        public Scope(int? ownerKey, string subject, string allowedAction)
        {
            OwnerKey = ownerKey;
            Subject = subject;
            AllowedAction = allowedAction;
        }

        // Deprecated: Should be removed once ApiKeys.cshtml is updated to support owner scope.
        public Scope(string subject, string allowedAction)
        {
            Subject = subject;
            AllowedAction = allowedAction;
        }
    }
}