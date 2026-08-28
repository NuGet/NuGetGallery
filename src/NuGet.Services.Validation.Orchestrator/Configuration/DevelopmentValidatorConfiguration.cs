// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    public class DevelopmentValidatorConfiguration
    {
        public bool Enabled { get; set; }

        public int DelaySeconds { get; set; }

        public string FailurePackageIdPrefix { get; set; }
    }
}
