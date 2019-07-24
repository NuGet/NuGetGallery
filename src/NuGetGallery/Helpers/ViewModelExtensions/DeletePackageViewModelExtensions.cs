// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class DeletePackageViewModelExtensions
    {
        public static DeletePackageViewModel Setup(
            this DeletePackageViewModel v,
            Package package,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons)
        {
            ((DisplayPackageViewModel)v).Setup(package, currentUser, deprecation: null);

            v.DeletePackagesRequest = new DeletePackagesRequest
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

            v.IsLocked = package.PackageRegistration.IsLocked;

            return v;
        }
    }
}