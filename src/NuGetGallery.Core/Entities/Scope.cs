// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class Scope
        : IEntity
    {
        public int Key { get; set; }

        public string Subject { get; set; }

        [Required]
        public string AllowedAction { get; set; }

        public virtual Credential Credential { get; set; }

        public Scope()
        {
        }

        public Scope(string allowedAction)
        {
            AllowedAction = allowedAction;
        }
    }
}