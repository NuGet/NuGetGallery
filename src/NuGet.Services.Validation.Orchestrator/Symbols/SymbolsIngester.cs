// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Symbols
{
    [ValidatorName(ValidatorName.SymbolsIngester)]
    public class SymbolsIngester : BaseNuGetValidator, INuGetValidator
    {
        private readonly ISymbolsValidationEntitiesService _symbolsValidationEntitiesService;
        private readonly ISymbolsIngesterMessageEnqueuer _symbolMessageEnqueuer;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<SymbolsIngester> _logger;

        public SymbolsIngester(
            ISymbolsValidationEntitiesService symbolsValidationEntitiesService,
            ISymbolsIngesterMessageEnqueuer symbolMessageEnqueuer,
            ITelemetryService telemetryService,
            ILogger<SymbolsIngester> logger)
        {
            _symbolsValidationEntitiesService = symbolsValidationEntitiesService ?? throw new ArgumentNullException(nameof(symbolsValidationEntitiesService));
            _symbolMessageEnqueuer = symbolMessageEnqueuer ?? throw new ArgumentNullException(nameof(symbolMessageEnqueuer));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var symbolsRequest = await _symbolsValidationEntitiesService.GetSymbolsServerRequestAsync(request);
            var response = SymbolsValidationEntitiesService.ToValidationResponse(symbolsRequest);
            _logger.LogInformation(
                    "Symbols status {Status} for PackageId: {PackageId}, PackageNormalizedVersion {PackageNormalizedVersion}, SymbolsPackageKey {SymbolsPackageKey} ValidationId {ValidationId}",
                    response.Status,
                    request.PackageId,
                    request.PackageVersion,
                    request.PackageKey,
                    request.ValidationId);

            return response;
        }

        /// <summary>
        /// The pattern used for the StartAsync:
        /// 1. Check if an ingestion for the specific symbols package key was already started
        /// 2. Only if a ingestion was not started queue the message to be processed.
        /// 3. After the message is queued, update the SymbolServerRequests table.
        /// </summary>
        /// <param name="request">The request to be sent to the ingester job queue.</param>
        /// <returns>The operation status as <see cref="INuGetValidationResponse"/>.</returns>
        public async Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var symbolsRequest = await _symbolsValidationEntitiesService.GetSymbolsServerRequestAsync(request);
            var response = SymbolsValidationEntitiesService.ToValidationResponse(symbolsRequest);

            if (response.Status != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Symbol ingestion for {PackageId} {PackageNormalizedVersion} {SymbolsPackageKey} has already started.",
                    request.PackageId,
                    request.PackageVersion,
                    request.PackageKey);

                return response;
            }

            _telemetryService.TrackSymbolsMessageEnqueued(request.PackageId, request.PackageVersion, ValidatorName.SymbolsIngester, request.ValidationId);
            var message = await _symbolMessageEnqueuer.EnqueueSymbolsIngestionMessageAsync(request);

            var newSymbolsRequest = SymbolsValidationEntitiesService.CreateFromValidationRequest(request, SymbolsPackageIngestRequestStatus.Ingesting, message.RequestName);
            var savedSymbolRequest = await _symbolsValidationEntitiesService.AddSymbolsServerRequestAsync(newSymbolsRequest);
            
            if(savedSymbolRequest.RequestStatusKey != SymbolsPackageIngestRequestStatus.Ingesting)
            {
                _logger.LogWarning(
                 "The symbols ingestion request already in the database. RequestStatus:{Status} for {PackageId} {PackageNormalizedVersion} {SymbolsPackageKey}.",
                 newSymbolsRequest.RequestStatusKey,
                 request.PackageId,
                 request.PackageVersion,
                 request.PackageKey);
            }
            else
            {
                _logger.LogInformation(
                 "The symbols ingestion request added to the database. RequestStatus:{Status} for {PackageId} {PackageNormalizedVersion} {SymbolsPackageKey}.",
                 newSymbolsRequest.RequestStatusKey,
                 request.PackageId,
                 request.PackageVersion,
                 request.PackageKey);
            }
            return SymbolsValidationEntitiesService.ToValidationResponse(savedSymbolRequest);
        }
    }
}
