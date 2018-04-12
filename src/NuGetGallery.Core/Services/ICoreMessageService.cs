// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public interface ICoreMessageService<T> where T : IEntity
    {
        void SendPackageAddedNotice(T package, string packageUrl, string packageSupportUrl, string emailSettingsUrl);
        void SendPackageValidationFailedNotice(T package, string packageUrl, string packageSupportUrl);
        void SendSignedPackageNotAllowedNotice(T package, string packageUrl, string announcementsUrl, string twitterUrl);
        void SendValidationTakingTooLongNotice(T package, string packageUrl);
    }
}