// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class MessageUpdater : IMessageUpdater
    {
        private readonly TimeSpan _eventStartMessageDelay;

        private readonly ITableWrapper _table;

        private readonly ILogger<MessageUpdater> _logger;

        public MessageUpdater(
            ITableWrapper table, 
            StatusAggregatorConfiguration configuration,
            ILogger<MessageUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _eventStartMessageDelay = TimeSpan.FromMinutes(configuration?.EventStartMessageDelayMinutes ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CreateMessageForEventStart(EventEntity eventEntity, DateTime cursor)
        {
            using (_logger.Scope("Creating message for start of event."))
            {
                if (cursor <= eventEntity.StartTime + _eventStartMessageDelay)
                {
                    // We don't want to show events that are too recent to avoid noisy events.
                    _logger.LogInformation("Event is too recent, cannot create message for its start.");
                    return;
                }

                if (_table.GetMessagesLinkedToEvent(eventEntity).ToList().Any())
                {
                    // If we've already told customers about an event, we don't need to tell them about it again.
                    _logger.LogInformation("Event has messages associated with it, cannot create message for its start.");
                    return;
                }

                var incidentsLinkedToEventQuery = _table.GetIncidentsLinkedToEvent(eventEntity);

                // We are querying twice here because table storage ignores rows where a column specified by a query is null.
                // MitigationTime is null when IsActive is true.
                // If we do not query separately here, rows where IsActive is true will be ignored in query results.

                var hasCurrentlyActiveIncidents = incidentsLinkedToEventQuery
                    .Where(i => i.IsActive)
                    .ToList()
                    .Any();

                var hasIncidentsActiveAtCursorTime = incidentsLinkedToEventQuery
                    .Where(i => i.MitigationTime >= cursor)
                    .ToList()
                    .Any();

                var hasActiveIncidents = hasCurrentlyActiveIncidents || hasIncidentsActiveAtCursorTime;
                if (!hasActiveIncidents)
                {
                    // If we haven't told customers about an event and it doesn't have any active incidents, it must have be linked to an incident that fired and was quickly mitigated.
                    // This event will be deactivated if it is not linked to an active incident soon.
                    // We will create a message when it is linked to another active incident.
                    _logger.LogInformation("Event has no active incidents, cannot create message for its start.");
                    return;
                }

                if (TryGetContentsForMessageForEventStart(eventEntity, out var contents))
                {
                    await CreateMessage(eventEntity, eventEntity.StartTime, contents);
                }
                else
                {
                    _logger.LogWarning("Failed to create a message for start of event!");
                }
            }
        }

        private const string _messageForEventStartTemplate = "**{0} is {1}.** You may encounter issues {2}.";

        private bool TryGetContentsForMessageForEventStart(EventEntity eventEntity, out string contents)
        {
            return TryGetContentsForEventHelper(eventEntity, _messageForEventStartTemplate, out contents);
        }

        public async Task CreateMessageForEventEnd(EventEntity eventEntity)
        {
            if (!eventEntity.EndTime.HasValue)
            {
                throw new ArgumentException("Must pass in an event with an end time!", nameof(eventEntity));
            }

            using (_logger.Scope("Creating message for end of event."))
            {
                if (!_table.GetMessagesLinkedToEvent(eventEntity).ToList().Any())
                {
                    // If we've never told customers about an event, we don't need to tell them it's no longer impacting them.
                    _logger.LogInformation("Event has no messages associated with it, cannot create message for its end.");
                    return;
                }

                if (TryGetContentsForMessageForEventEnd(eventEntity, out var contents))
                {
                    await CreateMessage(eventEntity, eventEntity.EndTime.Value, contents);
                }
                else
                {
                    _logger.LogWarning("Failed to create message!");
                }
            }
        }

        private Task CreateMessage(EventEntity eventEntity, DateTime time, string contents)
        {
            var messageEntity = new MessageEntity(eventEntity, time, contents);
            _logger.LogInformation("Creating message with time {MessageTimestamp} and contents {MessageContents}",
                messageEntity.Time, messageEntity.Contents);
            return _table.InsertOrReplaceAsync(messageEntity);
        }

        private const string _messageForEventEndTemplate = "**{0} is no longer {1}.** You should no longer encounter any issues {2}. Thank you for your patience.";

        private bool TryGetContentsForMessageForEventEnd(EventEntity eventEntity, out string contents)
        {
            return TryGetContentsForEventHelper(eventEntity, _messageForEventEndTemplate, out contents);
        }
        
        private bool TryGetContentsForEventHelper(
            EventEntity eventEntity, 
            string messageTemplate,
            out string contents)
        {
            contents = null;

            var path = eventEntity.AffectedComponentPath;
            var component = NuGetServiceComponentFactory.CreateNuGetServiceRootComponent().GetByPath(path);
            if (component == null)
            {
                _logger.LogWarning("Could not find a component with path {ComponentPath}.", path);
                return false;
            }

            var componentNames = path.Split(Constants.ComponentPathDivider);
            var componentName = string.Join(" ", componentNames.Skip(1).Reverse());
            var componentStatus = ((ComponentStatus)eventEntity.AffectedComponentStatus).ToString().ToLowerInvariant();

            string actionDescription = _actionDescriptionForComponentPathMap
                .FirstOrDefault(m => m.Matches(path))?
                .ActionDescription;

            if (actionDescription == null)
            {
                _logger.LogWarning("Could not find an action description for path {ComponentPath}.", path);
                return false;
            }

            contents = string.Format(messageTemplate, componentName, componentStatus, actionDescription);

            return !string.IsNullOrEmpty(contents);
        }

        private static readonly IEnumerable<ActionDescriptionForComponentPathPrefix> _actionDescriptionForComponentPathMap = new ActionDescriptionForComponentPathPrefix[]
        {
            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName),
                $"browsing the NuGet Gallery"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.ChinaRegionName),
                $"restoring packages from NuGet.org's V3 feed from China"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName),
                $"restoring packages from NuGet.org's V3 feed"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName),
                $"restoring packages from NuGet.org's V2 feed"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName),
                $"restoring packages"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName),
                $"searching for packages from China"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName),
                $"searching for packages"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName),
                "uploading new packages"),
        };

        private class ActionDescriptionForComponentPathPrefix
        {
            public string ComponentPathPrefix { get; }
            public string ActionDescription { get; }

            public ActionDescriptionForComponentPathPrefix(string componentPathPrefix, string actionDescription)
            {
                ComponentPathPrefix = componentPathPrefix;
                ActionDescription = actionDescription;
            }

            public bool Matches(string componentPath)
            {
                return componentPath.StartsWith(ComponentPathPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
