// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    /// <summary>
    /// The data associated with an event that affects the <see cref="MessageEntity"/>s associated with an <see cref="EventEntity"/>.
    /// </summary>
    public class MessageChangeEvent
    {
        public DateTime Timestamp { get; }
        public string AffectedComponentPath { get; }
        public ComponentStatus AffectedComponentStatus { get; }
        public MessageType Type { get; }

        public MessageChangeEvent(DateTime timestamp, string affectedComponentPath, ComponentStatus affectedComponentStatus, MessageType type)
        {
            Timestamp = timestamp;
            AffectedComponentPath = affectedComponentPath;
            AffectedComponentStatus = affectedComponentStatus;
            Type = type;
        }
    }
}