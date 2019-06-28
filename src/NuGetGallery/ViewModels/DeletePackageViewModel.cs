// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class DeletePackageViewModel : DisplayPackageViewModel
    {
        public DeletePackageViewModel(Package package, User currentUser, IReadOnlyList<ReportPackageReason> reasons)
        {
            // TODO: remove
            this.Setup(package, currentUser, reasons);
        }

        public IEnumerable<SelectListItem> VersionSelectList { get; set; }

        public DeletePackagesRequest DeletePackagesRequest { get; set; }

        public bool IsLocked { get; set; }
    }
}