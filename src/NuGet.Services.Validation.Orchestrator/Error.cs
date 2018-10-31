// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.Validation.Orchestrator
{
    public static class Error
    {
        public static EventId ConfigurationReadFailure = new EventId(1, "Failed to process configuration");
        public static EventId ConfigurationValidationFailure = new EventId(2, "Configuration is invalid");
        public static EventId OrchestratorOnMessageException = new EventId(6, "Failed to process orchestrator message");
        public static EventId UpdatingPackageDbStatusFailed = new EventId(7, "Failed to update package status in DB");

        public static EventId PackageSigningValidationAlreadyStarted = new EventId(100, "Package Signing validation already started");

        public static EventId PackageCertificateValidationAlreadyFailed = new EventId(200, "Package Signing state is already invalid");
        public static EventId PackageCertificateValidationInvalidSignatureState = new EventId(201, "Package Signature has invalid Status");

        // NOTE: EventIds 1000-1999 are reserved for the "Validation.PackageSigning.Core" project.
    }
}
