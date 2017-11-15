// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.Validation.Orchestrator
{
    public static class Error
    {
        public static EventId ConfigurationReadFailure = new EventId(1, "Failed to process configuration");
        public static EventId ConfigurationValidationFailure = new EventId(2, "Configuration is invalid");
        public static EventId VcsValidationAlreadyStarted = new EventId(3, "VCS validation already started");
        public static EventId VcsValidationFailureAuditFound = new EventId(4, "VCS validation failure audit found");
        public static EventId VcsValidationUnexpectedAuditFound = new EventId(5, "VCS validation unexpected audit found");
        public static EventId OrchestratorOnMessageException = new EventId(6, "Failed to process orchestrator message");
        public static EventId UpdatingPackageDbStatusFailed = new EventId(7, "Failed to update package status in DB");
    }
}
