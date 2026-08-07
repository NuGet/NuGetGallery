// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class StagedSymbolTerminalStateProcessor : IStagedSymbolTerminalStateProcessor
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly ILogger<StagedSymbolTerminalStateProcessor> _logger;

        public StagedSymbolTerminalStateProcessor(IEntitiesContext entitiesContext, ILogger<StagedSymbolTerminalStateProcessor> logger)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessAsync(PackageValidationSet validationSet, SymbolPackage symbolPackage, StagingArtifactStatus status)
        {
            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }

            if (status != StagingArtifactStatus.Ready && status != StagingArtifactStatus.ValidationFailed)
            {
                throw new ArgumentException(
                    $"A staged symbol validation can only transition to {nameof(StagingArtifactStatus.Ready)} or " +
                    $"{nameof(StagingArtifactStatus.ValidationFailed)}.",
                    nameof(status));
            }

            var artifact = _entitiesContext.StagedSymbolArtifacts.SingleOrDefault(a =>
                a.SymbolPackageKey == symbolPackage.Key
                && a.ValidationTrackingId == validationSet.ValidationTrackingId
                && a.ContentHash == symbolPackage.Hash
                && a.ParentContentHash == a.StagingEntry.Package.Hash
                && a.Status == StagingArtifactStatus.Validating);

            if (artifact == null)
            {
                _logger.LogInformation(
                    "Ignoring stale staged symbol outcome for symbol package key {SymbolPackageKey}, validation set {ValidationTrackingId}.",
                    symbolPackage.Key,
                    validationSet.ValidationTrackingId);
                return;
            }

            artifact.Status = status;
            artifact.ValidatedDate = status == StagingArtifactStatus.Ready ? DateTime.UtcNow : null;
            await _entitiesContext.SaveChangesAsync();
        }
    }
}
