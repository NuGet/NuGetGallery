// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    public class GitHubFederatedToken : IEntity
    {
        public int Key { get; set; }

        [Required]
        public string Organization { get; set; }

        [Required]
        public string Repository { get; set; }

        [Required]
        public string Branch { get; set; }

        public int UserKey { get; set; }

        public virtual User User { get; set; }

        [NotMapped]
        public string Subject => $"repo:{Organization}/{Repository}:ref:refs/heads/{Branch}";
    }
}
