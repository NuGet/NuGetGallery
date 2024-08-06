// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class ApiKeyOwnerViewModel
    {
        public ApiKeyOwnerViewModel(string owner, bool canPushNew, bool canPushExisting, bool canUnlist, IList<string> packageIds)
        {
            Owner = owner;
            CanPushNew = canPushNew;
            CanPushExisting = canPushExisting;
            CanUnlist = canUnlist;
            PackageIds = packageIds;
        }
        
        public string Owner { get; }
        public bool CanPushNew { get; }
        public bool CanPushExisting { get; }
        public bool CanUnlist { get; }
        public IList<string> PackageIds { get; }
    }
}