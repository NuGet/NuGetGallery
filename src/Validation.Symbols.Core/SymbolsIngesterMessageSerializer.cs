// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class SymbolsIngesterMessageSerializer : IBrokeredMessageSerializer<SymbolsIngesterMessage>
    {
        private const string SchemaName = "SymbolIngesterMessageData";

        private IBrokeredMessageSerializer<SymbolsIngesterMessageDataV1> _serializer =
            new BrokeredMessageSerializer<SymbolsIngesterMessageDataV1>();

        public SymbolsIngesterMessage Deserialize(IReceivedBrokeredMessage message)
        {
            var deserializedMessage = _serializer.Deserialize(message);

            return new SymbolsIngesterMessage(
                deserializedMessage.ValidationId,
                deserializedMessage.SymbolsPackageKey,
                deserializedMessage.PackageId,
                deserializedMessage.PackageNormalizedVersion,
                deserializedMessage.SnupkgUrl,
                deserializedMessage.RequestName);
        }

        public IBrokeredMessage Serialize(SymbolsIngesterMessage message)
            => _serializer.Serialize(new SymbolsIngesterMessageDataV1
            {
                ValidationId = message.ValidationId,
                SymbolsPackageKey = message.SymbolsPackageKey,
                PackageId = message.PackageId,
                PackageNormalizedVersion = message.PackageNormalizedVersion,
                SnupkgUrl = message.SnupkgUrl,
                RequestName = message.RequestName
            });

        [Schema(Name = SchemaName, Version = 1)]
        private class SymbolsIngesterMessageDataV1
        {
            public Guid ValidationId { get; set; }

            public int SymbolsPackageKey { get; set; }

            public string PackageId { get; set; }

            public string PackageNormalizedVersion { get; set; }

            public string SnupkgUrl { get; set; }

            public string RequestName { get; set; }
        }
    }
}
