// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Services
{
    public class TyposquattingOwnersDoubleCheck : ITyposquattingOwnersDoubleCheck
    {
        private static IPackageService PackageService { get; set; }

        public TyposquattingOwnersDoubleCheck(IPackageService packageService)
        {
            PackageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        public bool IsOwnerAllowedTyposquatting(string packageId, string userName)
        {
            var owners = PackageService.FindPackageRegistrationById(packageId).Owners;
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