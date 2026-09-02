// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.ServiceBus;
using Xunit;

namespace NuGetGallery
{
    public class StagedPackagePromotionMessageSerializerFacts
    {
        private const string SchemaNameKey = "SchemaName";
        private const string SchemaVersionKey = "SchemaVersion";
        private const string SchemaName = "ProcessStagedPackagePromotion";
        private const int SchemaVersion = 1;
        private const int StagedPackageKey = 456;

        private static readonly Guid PromotionId = new Guid("51e27465-48f7-4385-ae88-ff5394ab356f");
        private static readonly string MessageBody = $@"{{""PromotionId"":""{PromotionId}"",""StagedPackageKey"":{StagedPackageKey}}}";

        public class TheSerializeMethod
        {
            [Fact]
            public void ProducesExpectedMessage()
            {
                var target = new StagedPackagePromotionMessageSerializer();

                var output = target.Serialize(new StagedPackagePromotionMessage(PromotionId, StagedPackageKey));

                Assert.Equal(SchemaName, output.Properties[SchemaNameKey]);
                Assert.Equal(SchemaVersion, output.Properties[SchemaVersionKey]);
                Assert.Equal(MessageBody, output.GetBody());
            }
        }

        public class TheDeserializeMethod
        {
            [Fact]
            public void ProducesExpectedMessage()
            {
                var target = new StagedPackagePromotionMessageSerializer();
                var input = new Mock<IReceivedBrokeredMessage>();
                input.Setup(x => x.GetBody()).Returns(MessageBody);
                input.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, SchemaName },
                    { SchemaVersionKey, SchemaVersion },
                });

                var output = target.Deserialize(input.Object);

                Assert.Equal(PromotionId, output.PromotionId);
                Assert.Equal(StagedPackageKey, output.StagedPackageKey);
            }
        }
    }
}
