// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// The processor that strips unacceptable repository signatures and then validates signed packages.
    /// This runs before a package is repository signed.
    /// </summary>
    [ValidatorName(ValidatorName.PackageSignatureProcessor)]
    public class PackageSignatureProcessor : BaseSignatureProcessor, INuGetProcessor
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IProcessSignatureEnqueuer _signatureVerificationEnqueuer;
        private readonly ISimpleCloudBlobProvider _blobProvider;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackageSignatureProcessor> _logger;

        public PackageSignatureProcessor(
            IValidatorStateService validatorStateService,
            IProcessSignatureEnqueuer signatureVerificationEnqueuer,
            ISimpleCloudBlobProvider blobProvider,
            ITelemetryService telemetryService,
            ILogger<PackageSignatureProcessor> logger)
          : base(validatorStateService, signatureVerificationEnqueuer, blobProvider, telemetryService, logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _signatureVerificationEnqueuer = signatureVerificationEnqueuer ?? throw new ArgumentNullException(nameof(signatureVerificationEnqueuer));
            _blobProvider = blobProvider ?? throw new ArgumentNullException(nameof(blobProvider));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// This processor runs before packages are repository signed. Unacceptable
        /// repository signatures, if present, will be stripped.
        /// </summary>
        protected override bool RequiresRepositorySignature => false;
    }
}
