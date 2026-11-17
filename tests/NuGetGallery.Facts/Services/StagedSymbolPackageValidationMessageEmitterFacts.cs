// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery
{
    public class StagedSymbolPackageValidationMessageEmitterFacts
    {
        [Fact]
        public async Task EnqueuesStagedSymbolPackageValidation()
        {
            PackageValidationMessageData message = null;
            var enqueuer = new Mock<IPackageValidationEnqueuer>();
            enqueuer
                .Setup(x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()))
                .Callback<PackageValidationMessageData, DateTimeOffset>((value, _) => message = value)
                .Returns(Task.CompletedTask);
            var target = new StagedSymbolPackageValidationMessageEmitter(
                enqueuer.Object,
                Mock.Of<IAppConfiguration>(),
                Mock.Of<IDiagnosticsService>());
            var stagedSymbolPackage = new StagedSymbolPackage
            {
                Key = 43,
                StagedPackageIdentityKey = 42,
                StagedPackageIdentity = new StagedPackageIdentity
                {
                    Key = 42,
                    Package = new Package
                    {
                        Key = 42,
                        PackageRegistration = new PackageRegistration { Id = "PackageA" },
                        Version = "1.0.0",
                    },
                    OwnerKey = 1,
                    Owner = new User("owner") { Key = 1 },
                },
            };
            stagedSymbolPackage.StagedPackageIdentity.CurrentStagedSymbolPackageKey = stagedSymbolPackage.Key;
            stagedSymbolPackage.StagedPackageIdentity.CurrentStagedSymbolPackage = stagedSymbolPackage;

            var status = await target.StartValidationAsync(stagedSymbolPackage);

            Assert.Equal(StagedPackageStatus.Validating, status);
            Assert.Equal("PackageA", message.ProcessValidationSet.PackageId);
            Assert.Equal("1.0.0", message.ProcessValidationSet.PackageVersion);
            Assert.Equal(ValidatingType.StagedSymbolPackage, message.ProcessValidationSet.ValidatingType);
            Assert.Equal(stagedSymbolPackage.Key, message.ProcessValidationSet.EntityKey);
        }
    }
}
