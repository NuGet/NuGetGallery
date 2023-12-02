﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class LockPackageViewModel : LockViewModel
    {
        public LockPackageViewModel() : base("LockPackage", "Packages", "IDs")
        {
        }
    }
}