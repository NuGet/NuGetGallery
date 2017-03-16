// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Principal;

namespace NuGetGallery
{
    public interface ITelemetryService
    {
        void TrackODataQueryFilterEvent(string callContext, bool isEnabled, bool isAllowed, string queryPattern);

        void TrackPackagePushEvent(Package package, User user, IIdentity identity);

        void TrackSymbolsPushEvent(string packageId, string packageVersion, User user, IIdentity identity);

        void TrackSymbolsPushCallbackEvent(string packageId, string packageVersion, User user, IIdentity identity, int statusCode);
    }
}