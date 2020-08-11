// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class Auxiliary2AzureSearchCommand : IAzureSearchCommand
    {
        private readonly IAzureSearchCommand[] _commands;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<Auxiliary2AzureSearchCommand> _logger;

        public Auxiliary2AzureSearchCommand(
            IAzureSearchCommand updateVerifiedPackagesCommand,
            IAzureSearchCommand updateDownloadsCommand,
            IAzureSearchCommand updateOwnersCommand,
            IAzureSearchTelemetryService telemetryService,
            ILogger<Auxiliary2AzureSearchCommand> logger)
        {
            _commands = new[]
            {
                updateVerifiedPackagesCommand ?? throw new ArgumentNullException(nameof(updateVerifiedPackagesCommand)),
                updateDownloadsCommand ?? throw new ArgumentNullException(nameof(updateDownloadsCommand)),
                updateOwnersCommand ?? throw new ArgumentNullException(nameof(updateOwnersCommand)),
            };
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var outcome = JobOutcome.Failure;
            try
            {
                foreach (var command in _commands)
                {
                    _logger.LogInformation("Starting {CommandName}...", command.GetType().Name);
                    await command.ExecuteAsync();
                    _logger.LogInformation("Completed {CommandName}.", command.GetType().Name);
                }
                
                outcome = JobOutcome.Success;
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackAuxiliary2AzureSearchCompleted(outcome, stopwatch.Elapsed);
            }
        }
    }
}
