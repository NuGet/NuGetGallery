// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Entities
{
    public class Role
        : IEntity
    {
        public int Key { get; set; }
        public string Name { get; set; }
        public virtual ICollection<User> Users { get; set; }

        public Role()
        {
            Users = new HashSet<User>();
        }

        public bool Is(string roleName)
        {
            return string.Equals(Name, roleName, StringComparison.OrdinalIgnoreCase);
        }
    }
}