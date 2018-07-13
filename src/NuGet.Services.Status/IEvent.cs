// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Status
{
    /// <summary>
    /// Represents an event affecting a <see cref="IReadOnlyComponent"/>.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// The <see cref="IReadOnlyComponent.Path"/> of the <see cref="IReadOnlyComponent"/> affected.
        /// </summary>
        string AffectedComponentPath { get; }

        /// <summary>
        /// The <see cref="IReadOnlyComponent.Status"/> of the <see cref="IReadOnlyComponent"/> affected.
        /// </summary>
        ComponentStatus AffectedComponentStatus { get; }

        /// <summary>
        /// When the event began.
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// When the event ended, or <c>null</c> if the event is still active.
        /// </summary>
        DateTime? EndTime { get; }

        /// <summary>
        /// A set of <see cref="IMessage"/>s related to this event.
        /// </summary>
        IEnumerable<IMessage> Messages { get; }
    }
}
