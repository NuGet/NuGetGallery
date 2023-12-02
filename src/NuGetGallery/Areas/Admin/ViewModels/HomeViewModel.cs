// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class HomeViewModel
    {
        public HomeViewModel(bool showDatabaseAdmin, bool showLuceneAdmin, bool showValidation)
        {
            ShowDatabaseAdmin = showDatabaseAdmin;
            ShowLuceneAdmin = showLuceneAdmin;
            ShowValidation = showValidation;
        }

        public bool ShowDatabaseAdmin { get; }

        public bool ShowLuceneAdmin { get; }

        public bool ShowValidation { get; }
    }
}