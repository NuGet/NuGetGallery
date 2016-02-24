// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging;
using NuGet.Services.Gallery;
using Xunit;

namespace NuGetGallery.Commands
{
    public class AutomaticallyCuratePackageCommandFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public async Task WillCuratePackageUsingAllPackageCurators()
            {
                var firstStubCurator = new Mock<IAutomaticPackageCurator>();
                var secondStubCurator = new Mock<IAutomaticPackageCurator>();

                var cmd = new AutomaticallyCuratePackageCommand(new List<IAutomaticPackageCurator>()
                {
                    firstStubCurator.Object,
                    secondStubCurator.Object
                }, null);

                await cmd.ExecuteAsync(new Package(), new Mock<PackageArchiveReader>(TestPackage.CreateTestPackageStream("test", "1.0.0")).Object, commitChanges: true);

                firstStubCurator.Verify(stub => stub.CurateAsync(It.IsAny<Package>(), It.IsAny<PackageArchiveReader>(), true));
                secondStubCurator.Verify(stub => stub.CurateAsync(It.IsAny<Package>(), It.IsAny<PackageArchiveReader>(), true));
            }
        }
    }
}