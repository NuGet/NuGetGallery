// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ListPackageOwnerViewModel
    {
        public ListPackageOwnerViewModel()
        {
        }

        public ListPackageOwnerViewModel(User user)
        {
            Username = user.Username;
        }

        public string Username { get; set; }
    }
}