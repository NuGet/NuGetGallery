// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class DeleteAccountViewModel
    {
        private Lazy<bool> _hasOrphanPackages;

        public DeleteAccountViewModel()
        {
            _hasOrphanPackages = new Lazy<bool>(() => Packages.Any(p => p.HasSingleOwner));
        }

        public List<ListPackageItemViewModel> Packages { get; set; }

        public User User { get; set; }

        public string AccountName { get; set; }

        public bool HasPendingRequests { get; set; }

        public bool HasOrphanPackages
        {
            get
            {
                return Packages == null ? false : _hasOrphanPackages.Value;
            }
        }
    }
}