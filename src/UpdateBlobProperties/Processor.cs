// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UpdateBlobProperties
{
    public class Processor : IProcessor
    {
        private readonly ICollector _collector;
        private readonly IOptionsSnapshot<UpdateBlobPropertiesConfiguration> _configuration;
        private readonly Cursor _cursor;
        private readonly ILogger<Processor> _logger;

        public Processor(ICollector collector,
            IOptionsSnapshot<UpdateBlobPropertiesConfiguration> configuration,
            Cursor cursor,
            ILogger<Processor> logger)
        {
            _collector = collector;
            _configuration = configuration;
            _cursor = cursor;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _cursor.Load(token);
            _logger.LogInformation("Loaded the cursor value. ProcessedMaxKey: {cursorValue}.", _cursor.Value);

            var minKey = _cursor.Value + 1;
            var maxKey = _configuration.Value.MaxKey;
            _logger.LogInformation("Processing pages with minKey: {minKey} and maxKey: {maxKey}.", minKey, maxKey);

            await foreach (IList<PackageInfo> packageInfos in _collector.GetPagesOfPackageInfosAsync(minKey: minKey, maxKey: maxKey))
            {
                // Update blob properties
                // Next PR

                _cursor.Value = packageInfos.Last().Key;
                await _cursor.Save(token);
                _logger.LogInformation("Saved the cursor value. ProcessedMaxKey: {cursorValue}.", _cursor.Value);
            }
        }
    }
}
