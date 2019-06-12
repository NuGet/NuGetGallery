// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.ServiceBus;
using System;

namespace NuGetGallery.AccountDeleter
{
    public class AccoundDeleteMessageSerializer : IBrokeredMessageSerializer<AccountDeleteMessage>
    {
        private IBrokeredMessageSerializer<AccountDeleteMessage> _serializer = new BrokeredMessageSerializer<AccountDeleteMessage>();

        public AccountDeleteMessage Deserialize(IBrokeredMessage brokeredMessage)
        {
            var message = _serializer.Deserialize(brokeredMessage);
            return new AccountDeleteMessage()
            {
                Subject
            }
        }

        public IBrokeredMessage Serialize(AccountDeleteMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
