// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    /// <summary>
    /// Describes a message that was posted with type <see cref="MessageType.Start"/> for which a message with type <see cref="MessageType.End"/> has not been posted.
    /// For use by <see cref="IMessageChangeEventIterator"/>.
    /// </summary>
    public class ExistingStartMessageContext
    {
        public DateTime Timestamp { get; }
        public IComponent AffectedComponent { get; }
        public ComponentStatus AffectedComponentStatus { get; }

        public ExistingStartMessageContext(
            DateTime timestamp,
            IComponent affectedComponent,
            ComponentStatus affectedComponentStatus)
        {
            Timestamp = timestamp;
            AffectedComponent = affectedComponent;
            AffectedComponentStatus = affectedComponentStatus;
        }
    }
}