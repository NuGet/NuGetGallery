// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.ServiceBus
{
    public interface IBrokeredMessage : IDisposable
    {
        int DeliveryCount { get; }
        DateTimeOffset ExpiresAtUtc { get; }
        IDictionary<string, object> Properties { get; }
        DateTimeOffset EnqueuedTimeUtc { get; }
        DateTimeOffset ScheduledEnqueueTimeUtc { get; set; }
        Task CompleteAsync();
        Task AbandonAsync();
        string GetBody();
        IBrokeredMessage Clone();
    }
}