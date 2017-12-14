// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class ListPackageOwnerViewModel
    {
        public ListPackageOwnerViewModel()
        {
        }

        public ListPackageOwnerViewModel(User user)
        {
            IsOrganization = user is Organization;
            Username = user.Username;
        }

        public bool IsOrganization { get; set; }

        public string Username { get; set; }
    }
}