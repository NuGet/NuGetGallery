// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class RegistrationValidator : Validator<RegistrationEndpoint>
    {
        public RegistrationValidator(ValidatorConfiguration config, ILogger<RegistrationValidator> logger)
            : base(config, logger)
        {
        }
    }
}