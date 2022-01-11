// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class EventFactory : IComponentAffectingEntityFactory<EventEntity>
    {
        private readonly ITableWrapper _table;
        private readonly IAffectedComponentPathProvider<EventEntity> _pathProvider;

        private readonly ILogger<EventFactory> _logger;

        public EventFactory(
            ITableWrapper table,
            IAffectedComponentPathProvider<EventEntity> pathProvider,
            ILogger<EventFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EventEntity> CreateAsync(ParsedIncident input)
        {
            var affectedPath = _pathProvider.Get(input);
            _logger.LogInformation("Creating event for parsed incident with path {AffectedComponentPath}.", affectedPath);
            var entity = new EventEntity(affectedPath, input.StartTime);
            await _table.InsertOrReplaceAsync(entity);

            return entity;
        }
    }
}
