// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.ServiceBus
{
    public interface ISubscriptionClient
    {
        void OnMessageAsync(Func<IBrokeredMessage, Task> onMessageAsync);

        void OnMessageAsync(Func<IBrokeredMessage, Task> onMessageAsync, IOnMessageOptions options);

        Task CloseAsync();
    }
}