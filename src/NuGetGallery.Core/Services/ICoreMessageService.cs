// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public interface ICoreMessageService
    {
        void SendPackageAddedNotice(IPackage package, string packageUrl, string packageSupportUrl, string emailSettingsUrl);
        void SendPackageValidationFailedNotice(IPackage package, string packageUrl, string packageSupportUrl);
        void SendSignedPackageNotAllowedNotice(IPackage package, string packageUrl, string announcementsUrl, string twitterUrl);
        void SendValidationTakingTooLongNotice(IPackage package, string packageUrl);
    }
}