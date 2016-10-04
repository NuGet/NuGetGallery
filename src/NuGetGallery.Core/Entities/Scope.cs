// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class Scope
        : IEntity
    {
        [JsonIgnore]
        public int Key { get; set; }

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

        public Scope(string subject, string allowedAction)
        {
            Subject = subject;
            AllowedAction = allowedAction;
        }
    }
}