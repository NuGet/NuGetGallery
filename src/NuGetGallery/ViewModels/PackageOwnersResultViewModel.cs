// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class PackageOwnersResultViewModel
    {
        public string Name;

        public string EmailAddress;

        public bool Current;

        public bool Pending;

        public bool IsNamespaceOwner;

        public PackageOwnersResultViewModel(string username, string emailAddress, bool isCurrentUser, bool isPending, bool isNamespaceOwner)
        {
            Name = username;
            EmailAddress = emailAddress;
            Current = isCurrentUser;
            Pending = isPending;
            IsNamespaceOwner = isNamespaceOwner;
        }
    }
}