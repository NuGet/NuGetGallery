// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class LockPackageViewModel
    {
        public string Query { get; set; }

        public bool HasQuery => !string.IsNullOrEmpty(Query);

        public bool HasResults => PackageLockStates?.Count > 0;

        public IList<PackageLockState> PackageLockStates { get; set; }
    }

    public class PackageLockState
    {
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }
}