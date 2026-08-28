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
    public class StagedPackageValidationMessageEmitterFacts
    {
        [Fact]
        public async Task EnqueuesStagedPackageValidation()
        {
            PackageValidationMessageData message = null;
            var enqueuer = new Mock<IPackageValidationEnqueuer>();
            enqueuer
                .Setup(x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()))
                .Callback<PackageValidationMessageData, DateTimeOffset>((value, _) => message = value)
                .Returns(Task.CompletedTask);
            var target = new StagedPackageValidationMessageEmitter(
                enqueuer.Object,
                Mock.Of<IAppConfiguration>(),
                Mock.Of<IDiagnosticsService>());
            var stagedPackage = new StagedPackage
            {
                Key = 43,
                PackageKey = 42,
                Package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "PackageA" },
                    Version = "1.0.0",
                },
            };

            await target.StartValidationAsync(stagedPackage);

            Assert.Equal("PackageA", message.ProcessValidationSet.PackageId);
            Assert.Equal("1.0.0", message.ProcessValidationSet.PackageVersion);
            Assert.Equal(ValidatingType.StagedPackage, message.ProcessValidationSet.ValidatingType);
            Assert.Null(message.ProcessValidationSet.EntityKey);
        }
    }
}
