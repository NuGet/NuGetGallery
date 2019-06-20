// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.ServiceBus;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteMessageSerializer : IBrokeredMessageSerializer<AccountDeleteMessage>
    {
        private const string AccountDeleteMessageSchemaName = "AccountDeleteMessageData";

        private IBrokeredMessageSerializer<AccountDeleteMessageData> _serializer = new BrokeredMessageSerializer<AccountDeleteMessageData>();

        public AccountDeleteMessage Deserialize(IBrokeredMessage brokeredMessage)
        {
            var message = _serializer.Deserialize(brokeredMessage);

            return new AccountDeleteMessage(
                message.Username, 
                message.Source);
        }

        public IBrokeredMessage Serialize(AccountDeleteMessage message)
        {
            return _serializer.Serialize(new AccountDeleteMessageData
            {
                Username = message.Username,
                Source = message.Source
            });
        }

        [Schema(Name = AccountDeleteMessageSchemaName, Version = 1)]
        private struct AccountDeleteMessageData
        {
            public string Username { get; set; }

            public string Source { get; set; }
        }
    }
}
