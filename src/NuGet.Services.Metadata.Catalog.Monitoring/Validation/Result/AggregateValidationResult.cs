// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class AggregateValidationResult
    {
        public AggregateValidator AggregateValidator { get; set; }
        public IEnumerable<ValidationResult> ValidationResults { get; set; }
    }
}
