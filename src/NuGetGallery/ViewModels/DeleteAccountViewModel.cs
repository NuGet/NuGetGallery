// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class DeleteAccountViewModel
    {
        public List<ListPackageItemViewModel> Packages { get; set; }

        public User User { get; set; }

        public string AccountName { get; set; }

        public bool HasOrphanPackages { get; set; }
    }
}