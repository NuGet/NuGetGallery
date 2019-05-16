// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class HomeViewModel
    {
        public HomeViewModel(bool showDatabaseAdmin, bool showValidation)
        {
            ShowDatabaseAdmin = showDatabaseAdmin;
            ShowValidation = showValidation;
        }

        public bool ShowDatabaseAdmin { get; }

        public bool ShowValidation { get; }
    }
}