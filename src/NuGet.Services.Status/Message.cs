// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;

namespace NuGet.Services.Status
{
    /// <summary>
    /// A message associated with an <see cref="Event"/>.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// The time the message was posted.
        /// </summary>
        public DateTime Time { get; }

        /// <summary>
        /// The contents of the message.
        /// </summary>
        public string Contents { get; }

        [JsonConstructor]
        public Message(DateTime time, string contents)
        {
            Time = time;
            Contents = contents;
        }
    }
}
