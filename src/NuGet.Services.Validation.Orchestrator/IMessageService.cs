// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IMessageService
    {
        void SendPackagePublishedMessage(Package package);
        void SendPackageValidationFailedMessage(Package package);
        void SendPackageSignedValidationFailedMessage(Package package);
        void SendPackageValidationTakingTooLongMessage(Package package);
    }
}
