// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Moq;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery.Commands
{
    public class AutomaticallyCuratePackageCommandFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public void WillCuratePackageUsingAllPackageCurators()
            {
                var firstStubCurator = new Mock<IAutomaticPackageCurator>();
                var secondStubCurator = new Mock<IAutomaticPackageCurator>();

                var cmd = new AutomaticallyCuratePackageCommand(new List<IAutomaticPackageCurator>()
                {
                    firstStubCurator.Object,
                    secondStubCurator.Object
                }, null);
                

                cmd.Execute(new Package(), new Mock<INupkg>().Object, commitChanges: true);

                firstStubCurator.Verify(stub => stub.Curate(It.IsAny<Package>(), It.IsAny<INupkg>(), true));
                secondStubCurator.Verify(stub => stub.Curate(It.IsAny<Package>(), It.IsAny<INupkg>(), true));
            }
        }
    }
}