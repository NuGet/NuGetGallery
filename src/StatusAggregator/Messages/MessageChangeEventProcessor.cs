// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public class MessageChangeEventProcessor : IMessageChangeEventProcessor
    {
        private readonly IMessageFactory _factory;

        private readonly ILogger<MessageChangeEventProcessor> _logger;

        public MessageChangeEventProcessor(
            IMessageFactory factory,
            ILogger<MessageChangeEventProcessor> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<ExistingStartMessageContext> ProcessAsync(
            MessageChangeEvent change, 
            EventEntity eventEntity, 
            IComponent rootComponent, 
            ExistingStartMessageContext existingStartMessageContext)
        {
            _logger.LogInformation(
                "Processing change of type {StatusChangeType} at {StatusChangeTimestamp} affecting {StatusChangePath} with status {StatusChangeStatus}.",
                change.Type, change.Timestamp, change.AffectedComponentPath, change.AffectedComponentStatus);

            _logger.LogInformation("Getting component affected by change.");
            var component = rootComponent.GetByPath(change.AffectedComponentPath);
            if (component == null)
            {
                _logger.LogWarning("Affected path {change.AffectedComponentPath} does not exist in component tree.", nameof(change));
                return Task.FromResult(existingStartMessageContext);
            }

            switch (change.Type)
            {
                case MessageType.Start:
                    return ProcessStartMessageAsync(change, eventEntity, rootComponent, component, existingStartMessageContext);

                case MessageType.End:
                    return ProcessEndMessageAsync(change, eventEntity, rootComponent, component, existingStartMessageContext);

                default:
                    throw new ArgumentException($"Unexpected message type {change.Type}", nameof(change));
            }
        }

        private async Task<ExistingStartMessageContext> ProcessStartMessageAsync(
            MessageChangeEvent change, 
            EventEntity eventEntity, 
            IComponent rootComponent, 
            IComponent component, 
            ExistingStartMessageContext existingStartMessageContext)
        {
            _logger.LogInformation("Applying change to component tree.");
            component.Status = change.AffectedComponentStatus;

            // This change may affect a component that we do not display on the status page.
            // Find the deepester ancestor of the component that is directly affected.
            _logger.LogInformation("Determining if change affects visible component tree.");
            var lowestVisibleComponent = rootComponent.GetDeepestVisibleAncestorOfSubComponent(component);
            if (lowestVisibleComponent == null || lowestVisibleComponent.Status == ComponentStatus.Up)
            {
                // The change does not bubble up to a component that we display on the status page.
                // Therefore, we shouldn't post a message about it.
                _logger.LogInformation("Change does not affect visible component tree. Will not post or edit any messages.");
                return existingStartMessageContext;
            }

            // The change bubbles up to a component that we display on the status page.
            // We must post or update a message about it.
            if (existingStartMessageContext != null)
            {
                // There is an existing message we need to update.
                _logger.LogInformation("Found existing message, will edit it with information from new change.");
                // We must expand the scope of the existing message to include the component affected by this change.
                // In other words, if the message claims V2 Restore is down and V3 Restore is now down as well, we need to update the message to say Restore is down.
                var leastCommonAncestorPath = ComponentUtility.GetLeastCommonAncestorPath(existingStartMessageContext.AffectedComponent, lowestVisibleComponent);
                _logger.LogInformation("Least common ancestor component of existing message and this change is {LeastCommonAncestorPath}.", leastCommonAncestorPath);
                var leastCommonAncestor = rootComponent.GetByPath(leastCommonAncestorPath);
                if (leastCommonAncestor == null)
                {
                    // If the two components don't have a common ancestor, then they must not be a part of the same component tree.
                    // This should not be possible because it is asserted earlier that both these components are subcomponents of the root component.
                    throw new ArgumentException("Least common ancestor component of existing message and this change does not exist!", nameof(change));
                }

                if (leastCommonAncestor.Status == ComponentStatus.Up)
                {
                    // The least common ancestor of the component affected by the change and the component referred to by the existing message is unaffected!
                    // This should not be possible because the ancestor of any visible component should be visible (in other words, changes to visible components should always bubble up).
                    throw new ArgumentException("Least common ancestor of two visible components is unaffected!");
                }

                await _factory.UpdateMessageAsync(eventEntity, existingStartMessageContext.Timestamp, MessageType.Start, leastCommonAncestor);
                return new ExistingStartMessageContext(existingStartMessageContext.Timestamp, leastCommonAncestor, leastCommonAncestor.Status);
            }
            else
            {
                // There is not an existing message we need to update.
                _logger.LogInformation("No existing message found. Creating new start message for change.");
                await _factory.CreateMessageAsync(eventEntity, change.Timestamp, change.Type, lowestVisibleComponent);
                return new ExistingStartMessageContext(change.Timestamp, lowestVisibleComponent, lowestVisibleComponent.Status);
            }
        }

        private async Task<ExistingStartMessageContext> ProcessEndMessageAsync(
            MessageChangeEvent change,
            EventEntity eventEntity,
            IComponent rootComponent,
            IComponent component,
            ExistingStartMessageContext existingStartMessageContext)
        {
            _logger.LogInformation("Removing change from component tree.");
            component.Status = ComponentStatus.Up;

            if (existingStartMessageContext != null)
            {
                // There is an existing message that may be resolved by this change.
                // We should check if any visible components are still affected.
                _logger.LogInformation("Found existing message, testing if component tree is still affected.");

                var affectedSubComponents = existingStartMessageContext.AffectedComponent.GetAllVisibleComponents();
                if (affectedSubComponents.All(c => c.Status == ComponentStatus.Up))
                {
                    _logger.LogInformation("Component tree is no longer affected. Creating end message.");
                    await _factory.CreateMessageAsync(eventEntity, change.Timestamp, change.Type, existingStartMessageContext.AffectedComponent, existingStartMessageContext.AffectedComponentStatus);
                    return null;
                }
                else
                {
                    _logger.LogInformation("Component tree is still affected. Will not post an end message.");
                }
            }
            else
            {
                // There is no existing message.
                // We must have determined that we do not want to alert customers on this change.
                // The change likely affected a component that was not visible and did not bubble up.
                _logger.LogInformation("No existing message found. Will not add or delete any messages.");
            }

            return existingStartMessageContext;
        }
    }
}