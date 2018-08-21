// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class TyposquattingUserService : ITyposquattingUserService
    {
        private readonly IPackageService PackageService;

        public TyposquattingUserService(IPackageService packageService)
        {
            PackageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        public bool CanUserTyposquat(string packageId, string userName)
        {
            var package = PackageService.FindPackageRegistrationById(packageId);
            if (package == null)
            {
                return true;
            }

            var owners = package.Owners;
            foreach (var owner in owners)
            {
                if (owner.Username == userName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}