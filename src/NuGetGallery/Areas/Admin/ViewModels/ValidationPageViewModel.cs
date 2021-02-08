// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class ValidationPageViewModel
    {
        public ValidationPageViewModel() : this(null, null)
        {
        }

        public ValidationPageViewModel(string query, IReadOnlyList<NuGetPackageValidationViewModel> packages)
        {
            Query = query ?? string.Empty;
            Packages = packages ?? new List<NuGetPackageValidationViewModel>();
        }

        public string Query { get; }

        public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

        public bool HasResults => Packages.Count > 0;

        public IReadOnlyList<NuGetPackageValidationViewModel> Packages { get; }
    }
}