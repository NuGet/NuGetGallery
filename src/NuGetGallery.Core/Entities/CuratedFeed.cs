// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;

namespace NuGetGallery
{
    public class CuratedFeed : IEntity
    {
        public CuratedFeed()
        {
            Managers = new HashSet<User>();
            Packages = new HashSet<CuratedPackage>();
        }

        public string Name { get; set; }
        public virtual ICollection<User> Managers { get; set; }
        public virtual ICollection<CuratedPackage> Packages { get; set; }
        public int Key { get; set; }
    }
}