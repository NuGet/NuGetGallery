// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.Validation.Orchestrator
{
    public static class Error
    {
        public static EventId ConfigurationReadFailure = new EventId(1, "Failed to process configuration");
        public static EventId ConfigurationValidationFailure = new EventId(2, "Configuration is invalid");
    }
}
