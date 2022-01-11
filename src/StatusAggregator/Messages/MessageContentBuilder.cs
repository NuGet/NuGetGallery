// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;

namespace StatusAggregator.Messages
{
    public class MessageContentBuilder : IMessageContentBuilder
    {
        private readonly ILogger<MessageContentBuilder> _logger;

        public MessageContentBuilder(ILogger<MessageContentBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Build(
            MessageType type,
            IComponent component)
        {
            return Build(type, component, component.Status);
        }

        public string Build(
            MessageType type,
            IComponent component,
            ComponentStatus status)
        {
            return Build(type, component.Path, status);
        }

        private string Build(
            MessageType type,
            string path,
            ComponentStatus status)
        {
            _logger.LogInformation("Getting contents for message of type {MessageType} with path {ComponentPath} and status {ComponentStatus}.",
                type, path, status);

            if (!_messageTypeToMessageTemplate.TryGetValue(type, out string messageTemplate))
            {
                throw new ArgumentException("Could not find a template for type.", nameof(type));
            }

            _logger.LogInformation("Using template {MessageTemplate}.", messageTemplate);

            var nameString = GetName(path);
            _logger.LogInformation("Using {ComponentName} for name of component.", nameString);

            var actionDescription = GetActionDescriptionFromPath(path);
            if (actionDescription == null)
            {
                throw new ArgumentException("Could not find an action description for path.", nameof(path));
            }

            var statusString = status.ToString().ToLowerInvariant();
            var contents = string.Format(messageTemplate, nameString, statusString, actionDescription);
            _logger.LogInformation("Returned {Contents} for contents of message.", contents);
            return contents;
        }

        private string GetName(string path)
        {
            var componentNames = ComponentUtility.GetNames(path);
            return string.Join(" ", componentNames.Skip(1).Reverse());
        }

        private static readonly IDictionary<MessageType, string> _messageTypeToMessageTemplate = new Dictionary<MessageType, string>
        {
            { MessageType.Start, "**{0} is {1}.** You may encounter issues {2}." },
            { MessageType.End, "**{0} is no longer {1}.** You should no longer encounter any issues {2}. Thank you for your patience." },
        };

        private string GetActionDescriptionFromPath(string path)
        {
            return _actionDescriptionForComponentPathMap
                .FirstOrDefault(m => m.Matches(path))?
                .ActionDescription;
        }

        /// <remarks>
        /// This was not implemented as a dictionary because it is not possible to construct a <see cref="IEqualityComparer{T}.GetHashCode(T)"/> that works with component path prefixes.
        /// 
        /// Proof:
        /// A/B and A/C must have the same hashcode as A because A/B and A/C are both prefixed by A.
        /// However, A/B must not have the same hashcode as A/C because A/B is not a prefix of A/C and A/C is not a prefix of A/B.
        /// Therefore, A/B and A/C must have a hashcode that is both identical AND different.
        /// This is not possible.
        /// </remarks>
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