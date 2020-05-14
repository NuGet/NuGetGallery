// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.Validation
{
    public static class Error
    {
        public static EventId ValidatorStateServiceFailedToAddStatus = new EventId(1000, "Failed to add validator's status");
        public static EventId ValidatorStateServiceFailedToUpdateStatus = new EventId(1001, "Failed to update validator's status");
        public static EventId LoadedCertificateThumbprintDoesNotMatch = new EventId(1002, "Certificate thumbprint mismatch");
        public static EventId LoadCertificateFromStorageFailed = new EventId(1003, "Certificate loading from storage failed");

        public static EventId PackageHasUnsecureEntries = new EventId(1200, "The package archive with unsafe entries.");
        public static EventId PackagingException = new EventId(1201, "The package is not in the correct format.");
    }
}
