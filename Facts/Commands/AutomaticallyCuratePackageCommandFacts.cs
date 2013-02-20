﻿using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery.Commands
{
    public class AutomaticallyCuratePackageCommandFacts
    {
        public class TestableAutomaticallyCuratePackageCommand : AutomaticallyCuratePackageCommand
        {
            public TestableAutomaticallyCuratePackageCommand()
                : base(null)
            {
                StubCurators = new List<Mock<IAutomaticPackageCurator>>();
            }

            public List<Mock<IAutomaticPackageCurator>> StubCurators { get; private set; }

            protected override IEnumerable<T> GetServices<T>()
            {
                if (typeof(T) == typeof(IAutomaticPackageCurator))
                {
                    return StubCurators.Select(stub => (T)stub.Object);
                }

                throw new Exception("Tried to get unexpected servicves");
            }
        }

        public class TheExecuteMethod
        {
            [Fact]
            public void WillCuratePackageUsingAllPackageCurators()
            {
                var cmd = new TestableAutomaticallyCuratePackageCommand();
                var firstStubCurator = new Mock<IAutomaticPackageCurator>();
                var secondStubCurator = new Mock<IAutomaticPackageCurator>();
                cmd.StubCurators.Add(firstStubCurator);
                cmd.StubCurators.Add(secondStubCurator);

                cmd.Execute(new Package(), new Mock<IPackage>().Object, commitChanges: true);

                firstStubCurator.Verify(stub => stub.Curate(It.IsAny<Package>(), It.IsAny<IPackage>(), true));
                secondStubCurator.Verify(stub => stub.Curate(It.IsAny<Package>(), It.IsAny<IPackage>(), true));
            }
        }
    }
}