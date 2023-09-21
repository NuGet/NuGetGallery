// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.ServiceBus;

namespace NuGet.Services.Messaging
{
    public interface IServiceBusMessageSerializer
    {
        EmailMessageData DeserializeEmailMessageData(IReceivedBrokeredMessage message);
        IBrokeredMessage SerializeEmailMessageData(EmailMessageData message);
    }
}
