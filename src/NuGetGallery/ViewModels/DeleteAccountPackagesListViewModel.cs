﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class DeleteAccountPackagesListViewModel
    {
        public IEnumerable<DeleteAccountPackageItemViewModel> Packages { get; set; }

        public string Name { get; set; }

        public DeleteAccountPackagesListViewModel(IEnumerable<DeleteAccountPackageItemViewModel> packages, string name)
        {
            Packages = packages;
            Name = name;
        }
    }
}