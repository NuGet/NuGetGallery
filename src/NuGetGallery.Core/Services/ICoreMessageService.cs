// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using AnglicanGeek.MarkdownMailer;

namespace NuGetGallery.Services
{
    public interface ICoreMessageService
    {
        void SendPackageAddedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl);
    }
}