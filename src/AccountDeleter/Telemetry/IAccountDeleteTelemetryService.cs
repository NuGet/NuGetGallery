// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.AccountDeleter
{
    public interface IAccountDeleteTelemetryService
    {
        void TrackUserNotFound();

        void TrackException(Exception exception);

        void TrackSource(string source);

        void TrackDeleteResult(bool deleteSuccess);

        void TrackAccountDelete();

        void TrackEmailSent();
    }
}
