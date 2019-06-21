// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.AccountDeleter
{
    public interface IAccountDeleteTelemetryService
    {
        void TrackUserNotFound(string source);

        void TrackDeleteResult(string source, bool deleteSuccess);

        void TrackEmailSent(string source, bool contactAllowed);

        void TrackEmailBlocked(string source);

        void TrackUnknownSource(string source);
    }
}
