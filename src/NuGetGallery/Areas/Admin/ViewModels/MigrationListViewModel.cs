// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class MigrationListViewModel
    {
        public ICollection<MigrationViewModel> Applied { get; set; }
        public ICollection<MigrationViewModel> Pending { get; set; }
        public ICollection<MigrationViewModel> Available { get; set; }
    }
}