// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.AccountDeleter
{
    public interface IAccountDeleteTelemetryService
    {
        void TrackException(Exception exception);

        void TrackIncomingCommand(AccountDeleteMessage command);

        void TrackAccountDelete();

        void TrackEmailSent();
    }
}
