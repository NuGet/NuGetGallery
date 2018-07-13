// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.Status
{
    public class Event : IEvent
    {
        public string AffectedComponentPath { get; }
        public ComponentStatus AffectedComponentStatus { get; }
        public DateTime StartTime { get; }
        public DateTime? EndTime { get; }
        public IEnumerable<IMessage> Messages { get; }

        public Event(
            string affectedComponentPath,
            ComponentStatus affectedComponentStatus,
            DateTime startTime,
            DateTime? endTime,
            IEnumerable<IMessage> messages)
        {
            AffectedComponentPath = affectedComponentPath;
            AffectedComponentStatus = affectedComponentStatus;
            StartTime = startTime;
            EndTime = endTime;
            Messages = messages;
        }

        [JsonConstructor]
        public Event(
            string affectedComponentPath,
            ComponentStatus affectedComponentStatus, 
            DateTime startTime, 
            DateTime? endTime, 
            IEnumerable<Message> messages)
            : this(
                  affectedComponentPath, 
                  affectedComponentStatus, 
                  startTime, 
                  endTime, 
                  (IEnumerable<IMessage>)messages)
        {
        }
    }
}
