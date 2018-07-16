// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Areas.Admin.Services;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    using static RevalidationAdminService;

    public class RevalidationPageViewModel
    {
        public RevalidationPageViewModel(RevalidationSettings settings, RevalidationStatistics statistics)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        }
        
        public RevalidationSettings Settings { get; }
        public RevalidationStatistics Statistics { get; }
    }
}