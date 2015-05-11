// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public class Credential : IEntity
    {
        public Credential() { }

        public Credential(string type, string value)
        {
            Type = type;
            Value = value;
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

        public virtual User User { get; set; }
    }
}
