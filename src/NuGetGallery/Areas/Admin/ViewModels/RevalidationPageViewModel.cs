// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Areas.Admin.Services;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    using static RevalidationAdminService;

    public class RevalidationPageViewModel
    {
        public RevalidationPageViewModel(RevalidationState state, RevalidationStatistics statistics)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        }
        
        public RevalidationState State { get; }
        public RevalidationStatistics Statistics { get; }
    }
}