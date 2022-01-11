// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Manual;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class ManualStatusChangeHandler : IManualStatusChangeHandler
    {
        private readonly IDictionary<ManualStatusChangeType, IManualStatusChangeProcessor> _processorForType;

        private readonly ILogger<ManualStatusChangeHandler> _logger;

        public ManualStatusChangeHandler(
            IManualStatusChangeHandler<AddStatusEventManualChangeEntity> addStatusEventManualChangeHandler,
            IManualStatusChangeHandler<EditStatusEventManualChangeEntity> editStatusEventManualChangeHandler,
            IManualStatusChangeHandler<DeleteStatusEventManualChangeEntity> deleteStatusEventManualChangeHandler,
            IManualStatusChangeHandler<AddStatusMessageManualChangeEntity> addStatusMessageManualChangeHandler,
            IManualStatusChangeHandler<EditStatusMessageManualChangeEntity> editStatusMessageManualChangeHandler,
            IManualStatusChangeHandler<DeleteStatusMessageManualChangeEntity> deleteStatusMessageManualChangeHandler,
            ILogger<ManualStatusChangeHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _processorForType = new Dictionary<ManualStatusChangeType, IManualStatusChangeProcessor>
            {
                {
                    ManualStatusChangeType.AddStatusEvent,
                    new ManualStatusChangeProcessor<AddStatusEventManualChangeEntity>(
                        addStatusEventManualChangeHandler ?? throw new ArgumentNullException(nameof(addStatusEventManualChangeHandler)))
                },

                {
                    ManualStatusChangeType.EditStatusEvent,
                    new ManualStatusChangeProcessor<EditStatusEventManualChangeEntity>(
                        editStatusEventManualChangeHandler ?? throw new ArgumentNullException(nameof(editStatusEventManualChangeHandler)))
                },

                {
                    ManualStatusChangeType.DeleteStatusEvent,
                    new ManualStatusChangeProcessor<DeleteStatusEventManualChangeEntity>(
                        deleteStatusEventManualChangeHandler ?? throw new ArgumentNullException(nameof(deleteStatusEventManualChangeHandler)))
                },

                {
                    ManualStatusChangeType.AddStatusMessage,
                    new ManualStatusChangeProcessor<AddStatusMessageManualChangeEntity>(
                        addStatusMessageManualChangeHandler ?? throw new ArgumentNullException(nameof(addStatusMessageManualChangeHandler)))
                },

                {
                    ManualStatusChangeType.EditStatusMessage,
                    new ManualStatusChangeProcessor<EditStatusMessageManualChangeEntity>(
                        editStatusMessageManualChangeHandler ?? throw new ArgumentNullException(nameof(editStatusMessageManualChangeHandler)))
                },

                {
                    ManualStatusChangeType.DeleteStatusMessage,
                    new ManualStatusChangeProcessor<DeleteStatusMessageManualChangeEntity>(
                        deleteStatusMessageManualChangeHandler ?? throw new ArgumentNullException(nameof(deleteStatusMessageManualChangeHandler)))
                }
            };
        }

        public async Task Handle(ITableWrapper table, ManualStatusChangeEntity entity)
        {
            _logger.LogInformation("Handling manual status change at timestamp {ChangeTimestamp} with type {ChangeType}", entity.Timestamp, Enum.GetName(typeof(ManualStatusChangeType), entity.Type));
            try
            {
                var type = (ManualStatusChangeType)entity.Type;
                if (_processorForType.ContainsKey(type))
                {
                    await _processorForType[type].GetTask(table, entity);
                }
                else
                {
                    throw new ArgumentException("Invalid change type {ChangeType}! Cannot process manual status change!");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvents.ManualChangeFailure, e, "Failed to apply manual status change!");
            }
        }

        private interface IManualStatusChangeProcessor
        {
            Task GetTask(ITableWrapper table, ManualStatusChangeEntity entity);
        }

        /// <summary>
        /// Maps a <see cref="ManualStatusChangeEntity"/> to a <see cref="IManualStatusChangeHandler{T}"/>.
        /// </summary>
        private class ManualStatusChangeProcessor<T> : IManualStatusChangeProcessor
            where T : ManualStatusChangeEntity
        {
            private readonly IManualStatusChangeHandler<T> _handler;

            public ManualStatusChangeProcessor(IManualStatusChangeHandler<T> handler)
            {
                _handler = handler;
            }

            public async Task GetTask(ITableWrapper table, ManualStatusChangeEntity entity)
            {
                var typedEntity = await table.RetrieveAsync<T>(entity.RowKey);
                await _handler.Handle(typedEntity);
            }
        }
    }
}
