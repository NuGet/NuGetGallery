// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class FeatureFlagsViewModel
    {
        public FeatureFlagsViewModel(string flags, string contentId)
        {
            if (string.IsNullOrEmpty(flags))
            {
                throw new ArgumentException(nameof(flags));
            }

            if (string.IsNullOrEmpty(contentId))
            {
                throw new ArgumentException(nameof(contentId));
            }

            Flags = flags;
            ContentId = contentId;
        }

        public string Flags { get; }
        public string ContentId { get; }
    }
}