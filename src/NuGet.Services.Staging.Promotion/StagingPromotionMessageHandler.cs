// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;
using NuGetGallery;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Routes staging promotion commands to their target-specific handlers.
    /// </summary>
    public class StagingPromotionMessageHandler : IMessageHandler<StagingPromotionMessage>
    {
        private readonly IStagingPromotionMessageHandler<StagingGroup> _groupHandler;
        private readonly IStagingPromotionMessageHandler<StagedPackage> _packageHandler;
        private readonly Lazy<IStagingPromotionMessageHandler<StagedSymbolPackage>> _symbolHandler;
        private readonly IEntityRepository<StagedPackage> _packages;
        private readonly IEntityRepository<StagedSymbolPackage> _symbols;
        private readonly IStagingPromotionLease _lease;
        private readonly ILogger<StagingPromotionMessageHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingPromotionMessageHandler"/> class.
        /// </summary>
        /// <param name="groupHandler">The staging group promotion handler.</param>
        /// <param name="packageHandler">The staged package promotion handler.</param>
        /// <param name="symbolHandler">The staged symbol promotion dispatcher.</param>
        /// <param name="packages">The package attempts used to resolve the stable package identity.</param>
        /// <param name="symbols">The symbol attempts used to resolve the stable package identity.</param>
        /// <param name="lease">The scoped promotion lease.</param>
        /// <param name="logger">The logger.</param>
        public StagingPromotionMessageHandler(
            IStagingPromotionMessageHandler<StagingGroup> groupHandler,
            IStagingPromotionMessageHandler<StagedPackage> packageHandler,
            Lazy<IStagingPromotionMessageHandler<StagedSymbolPackage>> symbolHandler,
            IEntityRepository<StagedPackage> packages,
            IEntityRepository<StagedSymbolPackage> symbols,
            IStagingPromotionLease lease,
            ILogger<StagingPromotionMessageHandler> logger)
        {
            _groupHandler = groupHandler ?? throw new ArgumentNullException(nameof(groupHandler));
            _packageHandler = packageHandler ?? throw new ArgumentNullException(nameof(packageHandler));
            _symbolHandler = symbolHandler ?? throw new ArgumentNullException(nameof(symbolHandler));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
            _lease = lease ?? throw new ArgumentNullException(nameof(lease));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> HandleAsync(StagingPromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            try
            {
                switch (message.TargetType)
                {
                    case StagingPromotionTargetType.StagingGroup:
                        return await _lease.RunAsync($"group/{message.TargetKey}", () => _groupHandler.HandleAsync(message));
                    case StagingPromotionTargetType.StagedPackage:
                        var packageIdentity = _packages.GetAll().AsNoTracking()
                            .Where(package => package.Key == message.TargetKey)
                            .Select(package => (int?)package.StagedPackageIdentityKey)
                            .SingleOrDefault();
                        if (!packageIdentity.HasValue)
                        {
                            _logger.LogInformation("Ignoring promotion for missing package attempt {AttemptKey}.", message.TargetKey);
                            return true;
                        }

                        return await _lease.RunAsync($"package/{packageIdentity.Value}", () => _packageHandler.HandleAsync(message));
                    case StagingPromotionTargetType.StagedSymbolPackage:
                        var symbolIdentity = _symbols.GetAll().AsNoTracking()
                            .Where(symbols => symbols.Key == message.TargetKey)
                            .Select(symbols => (int?)symbols.StagedPackageIdentityKey)
                            .SingleOrDefault();
                        if (!symbolIdentity.HasValue)
                        {
                            _logger.LogInformation("Ignoring promotion for missing symbol attempt {AttemptKey}.", message.TargetKey);
                            return true;
                        }

                        return await _lease.RunAsync($"package/{symbolIdentity.Value}", () => _symbolHandler.Value.HandleAsync(message));
                    default:
                        throw new InvalidOperationException($"Unknown staging promotion target type '{message.TargetType}'.");
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to process staging promotion {PromotionId} for {TargetType} target {TargetKey}.",
                    message.PromotionId,
                    message.TargetType,
                    message.TargetKey);
                throw;
            }
        }
    }
}
