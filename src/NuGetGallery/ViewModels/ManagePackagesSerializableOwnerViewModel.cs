// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ManagePackagesSerializableOwnerViewModel
    {
        public ManagePackagesSerializableOwnerViewModel(User user, RouteUrlTemplate<User> profileUrlTemplate)
        {
            Username = user.Username;
            TrimmedUsername = Username.Abbreviate(15);
            ProfileUrl = profileUrlTemplate.Resolve(user);
            IsOrganization = user is Organization;
        }

        public string Username { get; }
        public string TrimmedUsername { get; }
        public string ProfileUrl { get; }
        public bool IsOrganization { get; }
    }
}