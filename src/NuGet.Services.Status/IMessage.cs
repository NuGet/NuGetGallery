// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Status
{
    /// <summary>
    /// A message associated with an <see cref="IEvent"/>.
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// The time the message was posted.
        /// </summary>
        DateTime Time { get; }

        /// <summary>
        /// The contents of the message.
        /// </summary>
        string Contents { get; }
    }
}
