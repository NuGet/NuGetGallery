// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class DeletePackageViewModel : DisplayPackageViewModel
    {
        public DeletePackageViewModel(Package package, User currentUser, IReadOnlyList<ReportPackageReason> reasons)
            : base(package, currentUser)
        {
            DeletePackagesRequest = new DeletePackagesRequest
            {
                Packages = new List<string>
                {
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}|{1}",
                        package.PackageRegistration.Id,
                        package.Version)
                },
                ReasonChoices = reasons
            };

            IsLocked = package.PackageRegistration.IsLocked;
        }

        public SelectList VersionSelectList { get; set; }

        public DeletePackagesRequest DeletePackagesRequest { get; set; }

        public bool IsLocked { get; }
    }
}