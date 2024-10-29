// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PackageRegistrationUserChangeModel
    {
        public PackageRegistrationUserChangeModel(PackageOwnershipState state, User owner, PackageOwnerRequest request)
        {
            State = state;
            Owner = owner;
            Request = request;
        }

        public PackageOwnershipState State { get; }
        public User Owner { get; }
        public PackageOwnerRequest Request { get; }
    }
}