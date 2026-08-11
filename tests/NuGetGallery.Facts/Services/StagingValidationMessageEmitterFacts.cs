// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery
{
    public class StagingValidationMessageEmitterFacts
    {
        [Fact]
        public async Task EnqueuesPackageWithExactTrackingIdAndEntityKey()
        {
            var facts = new Facts();
            var trackingId = Guid.NewGuid();
            var package = Facts.CreatePackage();

            await facts.Target.StartValidationAsync(package, trackingId);

            var message = Assert.Single(facts.PackageMessages).ProcessValidationSet;
            Assert.Equal(trackingId, message.ValidationTrackingId);
            Assert.Equal(package.Key, message.EntityKey);
            Assert.Equal(ValidatingType.Package, message.ValidatingType);
            Assert.Equal(PackageStatus.Staged, package.PackageStatusKey);
            Assert.Empty(facts.SymbolMessages);
        }

        [Fact]
        public async Task EnqueuesSymbolPackageWithExactTrackingIdAndEntityKey()
        {
            var facts = new Facts();
            var trackingId = Guid.NewGuid();
            var package = Facts.CreatePackage();
            var symbolPackage = new SymbolPackage
            {
                Key = 456,
                Package = package,
                PackageKey = package.Key,
                StatusKey = PackageStatus.Staged,
            };

            await facts.Target.StartValidationAsync(symbolPackage, trackingId);

            var message = Assert.Single(facts.SymbolMessages).ProcessValidationSet;
            Assert.Equal(trackingId, message.ValidationTrackingId);
            Assert.Equal(symbolPackage.Key, message.EntityKey);
            Assert.Equal(ValidatingType.SymbolPackage, message.ValidatingType);
            Assert.Equal(PackageStatus.Staged, symbolPackage.StatusKey);
            Assert.Empty(facts.PackageMessages);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task RejectsPackageWithoutAssignedEntityKey(int key)
        {
            var facts = new Facts();
            var package = Facts.CreatePackage();
            package.Key = key;

            await Assert.ThrowsAsync<InvalidOperationException>(() => facts.Target.StartValidationAsync(package, Guid.NewGuid()));

            Assert.Empty(facts.PackageMessages);
        }

        [Fact]
        public async Task RejectsEntityThatIsNotStaged()
        {
            var facts = new Facts();
            var package = Facts.CreatePackage();
            package.PackageStatusKey = PackageStatus.Validating;

            await Assert.ThrowsAsync<InvalidOperationException>(() => facts.Target.StartValidationAsync(package, Guid.NewGuid()));

            Assert.Empty(facts.PackageMessages);
        }

        private sealed class Facts
        {
            public Facts()
            {
                PackageMessages = new List<PackageValidationMessageData>();
                SymbolMessages = new List<PackageValidationMessageData>();
                var packageEnqueuer = CreateEnqueuer(PackageMessages);
                var symbolEnqueuer = CreateEnqueuer(SymbolMessages);
                var appConfiguration = new Mock<IAppConfiguration>();
                var diagnosticsService = new Mock<IDiagnosticsService>();

                Target = new StagingValidationMessageEmitter(packageEnqueuer.Object, symbolEnqueuer.Object, appConfiguration.Object, diagnosticsService.Object);
            }

            public StagingValidationMessageEmitter Target { get; }
            public List<PackageValidationMessageData> PackageMessages { get; }
            public List<PackageValidationMessageData> SymbolMessages { get; }

            public static Package CreatePackage()
            {
                return new Package
                {
                    Key = 123,
                    PackageRegistration = new PackageRegistration { Id = "Package" },
                    Version = "1.2.3",
                    PackageStatusKey = PackageStatus.Staged,
                };
            }

            private static Mock<IPackageValidationEnqueuer> CreateEnqueuer(List<PackageValidationMessageData> messages)
            {
                var enqueuer = new Mock<IPackageValidationEnqueuer>();
                enqueuer
                    .Setup(x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()))
                    .Callback<PackageValidationMessageData, DateTimeOffset>((message, _) => messages.Add(message))
                    .Returns(Task.CompletedTask);
                return enqueuer;
            }
        }
    }
}
