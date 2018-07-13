// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;

namespace NuGet.Services.Status
{
    public class Message : IMessage
    {
        public DateTime Time { get; }
        public string Contents { get; }

        [JsonConstructor]
        public Message(DateTime time, string contents)
        {
            Time = time;
            Contents = contents;
        }
    }
}
