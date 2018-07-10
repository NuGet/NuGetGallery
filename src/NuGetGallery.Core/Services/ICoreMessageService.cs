// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Validation;

namespace NuGetGallery.Services
{
    public interface ICoreMessageService
    {
        void SendPackageAddedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null);
        void SendPackageValidationFailedNotice(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl);
        void SendValidationTakingTooLongNotice(Package package, string packageUrl);
    }
}