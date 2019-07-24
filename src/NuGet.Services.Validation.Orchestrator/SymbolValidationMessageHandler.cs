// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// The message handler for Symbols.
    /// </summary>
    public class SymbolValidationMessageHandler : BaseValidationMessageHandler<SymbolPackage>
    {
        public SymbolValidationMessageHandler(
            IOptionsSnapshot<ValidationConfiguration> validationConfigsAccessor,
            IEntityService<SymbolPackage> entityService,
            IValidationSetProvider<SymbolPackage> validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor<SymbolPackage> validationOutcomeProcessor,
            ITelemetryService telemetryService,
            ILogger<SymbolValidationMessageHandler> logger) : base(
                validationConfigsAccessor,
                entityService,
                validationSetProvider,
                validationSetProcessor,
                validationOutcomeProcessor,
                telemetryService,
                logger)
        {
        }

        protected override ValidatingType ValidatingType => ValidatingType.SymbolPackage;
        protected override bool ShouldNoOpDueToDeletion => false;
    }
}
