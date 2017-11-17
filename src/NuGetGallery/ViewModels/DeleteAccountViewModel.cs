// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class DeleteAccountViewModel
    {
        private bool? _hasOrphanPackages;

        public List<ListPackageItemViewModel> Packages { get; set; }

        public User User { get; set; }

        public string AccountName { get; set; }

        public bool HasPendingRequests { get; set; }

        public bool HasOrphanPackages
        {
            get
            {
                if (!_hasOrphanPackages.HasValue)
                {
                    _hasOrphanPackages = Packages?.Any(p => p.HasSingleOwner);
                }
                return _hasOrphanPackages ?? false;
            }
        }
    }
}