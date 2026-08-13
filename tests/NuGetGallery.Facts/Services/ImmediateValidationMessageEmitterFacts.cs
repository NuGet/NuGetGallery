// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class ImmediateValidationMessageEmitterFacts
    {
        public class TheStartValidationAsyncMethod
        {
            [Fact]
            public async Task ReturnsAvailable()
            {
                // Arrange
                var package = new Package();
                var target = new ImmediateValidationMessageEmitter<IPackageEntity>();

                // Act
                var actual = await target.StartValidationAsync(package);

                // Assert
                Assert.Equal(PackageStatus.Available, actual);
            }
        }
    }
}
