// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.ServiceBus
{
    public interface IBrokeredMessage
    {
        TimeSpan TimeToLive { get; set; }
        IDictionary<string, object> Properties { get; }
        DateTimeOffset ScheduledEnqueueTimeUtc { get; set; }
        string MessageId { get; set; }
        string GetBody();
        TStream GetBody<TStream>();
    }
}