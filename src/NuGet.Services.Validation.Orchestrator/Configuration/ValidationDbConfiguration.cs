// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationDbConfiguration : IDbConfiguration
    {
        public string ConnectionString { get; set; }
    }
}
