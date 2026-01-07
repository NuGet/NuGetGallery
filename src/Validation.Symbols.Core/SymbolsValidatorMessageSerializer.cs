// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class SymbolsValidatorMessageSerializer : IBrokeredMessageSerializer<SymbolsValidatorMessage>
    {
        private const string SchemaName = "SymbolsValidatorMessageData";

        private IBrokeredMessageSerializer<SymbolsValidatorMessageDataV1> _serializer =
            new BrokeredMessageSerializer<SymbolsValidatorMessageDataV1>();

        public SymbolsValidatorMessage Deserialize(IReceivedBrokeredMessage message)
        {
            var deserializedMessage = _serializer.Deserialize(message);

            return new SymbolsValidatorMessage(
                deserializedMessage.ValidationId,
                deserializedMessage.SymbolsPackageKey,
                deserializedMessage.PackageId,
                deserializedMessage.PackageNormalizedVersion,
                deserializedMessage.SnupkgUrl);
        }

        public IBrokeredMessage Serialize(SymbolsValidatorMessage message)
            => _serializer.Serialize(new SymbolsValidatorMessageDataV1
            {
                ValidationId = message.ValidationId,
                SymbolsPackageKey = message.SymbolsPackageKey,
                PackageId = message.PackageId,
                PackageNormalizedVersion = message.PackageNormalizedVersion,
                SnupkgUrl = message.SnupkgUrl
            });

        [Schema(Name = SchemaName, Version = 1)]
        private class SymbolsValidatorMessageDataV1
        {
            public Guid ValidationId { get; set; }

            public int SymbolsPackageKey { get; set; }

            public string PackageId { get; set; }

            public string PackageNormalizedVersion { get; set; }

            public string SnupkgUrl { get; set; }
        }
    }
}
