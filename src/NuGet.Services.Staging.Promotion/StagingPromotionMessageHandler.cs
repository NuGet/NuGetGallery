// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Routes staging promotion commands to their target-specific handlers.
    /// </summary>
    public class StagingPromotionMessageHandler : IMessageHandler<StagingPromotionMessage>
    {
        private readonly IStagingPromotionMessageHandler<StagingGroup> _groupHandler;
        private readonly IStagingPromotionMessageHandler<StagedPackage> _packageHandler;
        private readonly ILogger<StagingPromotionMessageHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingPromotionMessageHandler"/> class.
        /// </summary>
        /// <param name="groupHandler">The staging group promotion handler.</param>
        /// <param name="packageHandler">The staged package promotion handler.</param>
        /// <param name="logger">The logger.</param>
        public StagingPromotionMessageHandler(
            IStagingPromotionMessageHandler<StagingGroup> groupHandler,
            IStagingPromotionMessageHandler<StagedPackage> packageHandler,
            ILogger<StagingPromotionMessageHandler> logger)
        {
            _groupHandler = groupHandler ?? throw new ArgumentNullException(nameof(groupHandler));
            _packageHandler = packageHandler ?? throw new ArgumentNullException(nameof(packageHandler));
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
                        return await _groupHandler.HandleAsync(message);
                    case StagingPromotionTargetType.StagedPackage:
                        return await _packageHandler.HandleAsync(message);
                    case StagingPromotionTargetType.StagedSymbolPackage:
                        throw new NotSupportedException("Staged symbol package promotion is not implemented.");
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
