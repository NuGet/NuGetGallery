// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class TyposquattingUserService : ITyposquattingUserService
    {
        private readonly IPackageService _packageService;

        public TyposquattingUserService(IPackageService packageService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        public bool CanUserTyposquat(string packageId, string userName)
        {
            var package = _packageService.FindPackageRegistrationById(packageId);
            if (package == null)
            {
                return false;
            }

            var owners = package.Owners;
            foreach (var owner in owners)
            {
                // TODO: handle the package which is owned by an organization. 
                // https://github.com/NuGet/Engineering/issues/1656
                if (owner.Username == userName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}