// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;
using Xunit;

namespace NuGetGallery
{
    public class StagingPromotionMessageSerializerFacts
    {
        private const string SchemaNameKey = "SchemaName";
        private const string SchemaVersionKey = "SchemaVersion";
        private const string SchemaName = "ProcessStagingPromotion";
        private const int SchemaVersion = 1;
        private const int StagingGroupKey = 123;
        private const int StagedPackageKey = 456;
        private const int StagedSymbolPackageKey = 789;

        private static readonly Guid PromotionId = new Guid("51e27465-48f7-4385-ae88-ff5394ab356f");
        private static readonly string PackageMessageBody = $@"{{""PromotionId"":""{PromotionId}"",""TargetType"":2,""TargetKey"":{StagedPackageKey}}}";
        private static readonly string GroupMessageBody = $@"{{""PromotionId"":""{PromotionId}"",""TargetType"":1,""TargetKey"":{StagingGroupKey}}}";
        private static readonly string SymbolPackageMessageBody = $@"{{""PromotionId"":""{PromotionId}"",""TargetType"":3,""TargetKey"":{StagedSymbolPackageKey}}}";

        public class TheSerializeMethod
        {
            [Fact]
            public void ProducesExpectedPackageMessage()
            {
                var target = new StagingPromotionMessageSerializer();

                var output = target.Serialize(StagingPromotionMessage.ForPackage(PromotionId, StagedPackageKey));

                Assert.Equal(SchemaName, output.Properties[SchemaNameKey]);
                Assert.Equal(SchemaVersion, output.Properties[SchemaVersionKey]);
                Assert.Equal(PackageMessageBody, output.GetBody());
            }

            [Fact]
            public void ProducesExpectedGroupMessage()
            {
                var target = new StagingPromotionMessageSerializer();

                var output = target.Serialize(StagingPromotionMessage.ForGroup(PromotionId, StagingGroupKey));

                Assert.Equal(SchemaName, output.Properties[SchemaNameKey]);
                Assert.Equal(SchemaVersion, output.Properties[SchemaVersionKey]);
                Assert.Equal(GroupMessageBody, output.GetBody());
            }

            [Fact]
            public void ProducesExpectedSymbolPackageMessage()
            {
                var target = new StagingPromotionMessageSerializer();

                var output = target.Serialize(StagingPromotionMessage.ForSymbolPackage(PromotionId, StagedSymbolPackageKey));

                Assert.Equal(SchemaName, output.Properties[SchemaNameKey]);
                Assert.Equal(SchemaVersion, output.Properties[SchemaVersionKey]);
                Assert.Equal(SymbolPackageMessageBody, output.GetBody());
            }
        }

        public class TheDeserializeMethod
        {
            [Fact]
            public void ProducesExpectedPackageMessage()
            {
                var target = new StagingPromotionMessageSerializer();
                var input = new Mock<IReceivedBrokeredMessage>();
                input.Setup(x => x.GetBody()).Returns(PackageMessageBody);
                input.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, SchemaName },
                    { SchemaVersionKey, SchemaVersion },
                });

                var output = target.Deserialize(input.Object);

                Assert.Equal(PromotionId, output.PromotionId);
                Assert.Equal(StagingPromotionTargetType.StagedPackage, output.TargetType);
                Assert.Equal(StagedPackageKey, output.TargetKey);
            }

            [Fact]
            public void ProducesExpectedGroupMessage()
            {
                var target = new StagingPromotionMessageSerializer();
                var input = new Mock<IReceivedBrokeredMessage>();
                input.Setup(x => x.GetBody()).Returns(GroupMessageBody);
                input.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, SchemaName },
                    { SchemaVersionKey, SchemaVersion },
                });

                var output = target.Deserialize(input.Object);

                Assert.Equal(PromotionId, output.PromotionId);
                Assert.Equal(StagingPromotionTargetType.StagingGroup, output.TargetType);
                Assert.Equal(StagingGroupKey, output.TargetKey);
            }

            [Fact]
            public void ProducesExpectedSymbolPackageMessage()
            {
                var target = new StagingPromotionMessageSerializer();
                var input = new Mock<IReceivedBrokeredMessage>();
                input.Setup(x => x.GetBody()).Returns(SymbolPackageMessageBody);
                input.Setup(x => x.Properties).Returns(new Dictionary<string, object>
                {
                    { SchemaNameKey, SchemaName },
                    { SchemaVersionKey, SchemaVersion },
                });

                var output = target.Deserialize(input.Object);

                Assert.Equal(PromotionId, output.PromotionId);
                Assert.Equal(StagingPromotionTargetType.StagedSymbolPackage, output.TargetType);
                Assert.Equal(StagedSymbolPackageKey, output.TargetKey);
            }

        }
    }
}
