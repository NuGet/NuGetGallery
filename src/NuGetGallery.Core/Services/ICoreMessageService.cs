// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Validation;
using System.Threading.Tasks;

namespace NuGetGallery.Services
{
    public interface ICoreMessageService
    {
        Task SendPackageAddedNoticeAsync(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null);
        Task SendPackageAddedWithWarningsNoticeAsync(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages);
        Task SendPackageValidationFailedNoticeAsync(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl);
        Task SendValidationTakingTooLongNoticeAsync(Package package, string packageUrl);
    }
}