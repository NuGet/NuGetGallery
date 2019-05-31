// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class StatusViewModel
    {
        public StatusViewModel(
            bool sqlAzureAvailable,
            bool? storageAvailable)
        {
            SqlAzureAvailable = sqlAzureAvailable;
            StorageAvailable = storageAvailable;
        }

        public bool GalleryServiceAvailable => SqlAzureAvailable && (StorageAvailable ?? true);

        public bool SqlAzureAvailable { get; }
        public bool? StorageAvailable { get; }
    }
}