// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;

namespace StatusAggregator.Messages
{
    public class MessageChangeEventIterator : IMessageChangeEventIterator
    {
        private readonly IComponentFactory _factory;
        private readonly IMessageChangeEventProcessor _processor;

        private readonly ILogger<MessageChangeEventIterator> _logger;

        public MessageChangeEventIterator(
            IComponentFactory factory,
            IMessageChangeEventProcessor processor,
            ILogger<MessageChangeEventIterator> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task IterateAsync(IEnumerable<MessageChangeEvent> changes, EventEntity eventEntity)
        {
            var rootComponent = _factory.Create();
            ExistingStartMessageContext existingStartMessageContext = null;
            foreach (var change in changes.OrderBy(c => c.Timestamp))
            {
                existingStartMessageContext = await _processor.ProcessAsync(change, eventEntity, rootComponent, existingStartMessageContext);
            }
        }
    }
}