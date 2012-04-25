using System;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class CreatedCuratedPackageCommandFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public void WillThrowWhenCuratedFeedDoesNotExist()
            {
                var cmd = new TestableCreateCuratedPackageCommand();
                cmd.StubCuratedFeedByKeyQry
                    .Setup(stub => stub.Execute(It.IsAny<int>(), It.IsAny<bool>()))
                    .Returns((CuratedFeed)null);

                Assert.Throws<InvalidOperationException>(() => cmd.Execute(
                    0,
                    0));
            }

            [Fact]
            public void WillThrowWhenPackageRegistrationDoesNotExist()
            {
                var cmd = new TestableCreateCuratedPackageCommand();
                cmd.StubPackageRegistrationByKeyQry
                    .Setup(stub => stub.Execute(It.IsAny<int>(), It.IsAny<bool>()))
                    .Returns((PackageRegistration)null);

                Assert.Throws<InvalidOperationException>(() => cmd.Execute(
                    0,
                    0));
            }

            [Fact]
            public void WillAddANewCuratedPackageToTheCuratedFeed()
            {
                var cmd = new TestableCreateCuratedPackageCommand();
                cmd.StubPackageRegistration.Key = 1066;

                cmd.Execute(
                    42,
                    1066,
                    false,
                    true,
                    "theNotes");

                var curatedPackage = cmd.StubCuratedFeed.Packages.First();
                Assert.Equal(1066, curatedPackage.PackageRegistrationKey);
                Assert.Equal(false, curatedPackage.Included);
                Assert.Equal(true, curatedPackage.AutomaticallyCurated);
                Assert.Equal("theNotes", curatedPackage.Notes);
                
            }

            [Fact]
            public void WillSaveTheEntityChanges()
            {
                var cmd = new TestableCreateCuratedPackageCommand();

                cmd.Execute(
                    0,
                    0);

                cmd.StubEntitiesContext.Verify(stub => stub.SaveChanges());
            }

            [Fact]
            public void WillReturnTheCreatedCuratedPackage()
            {
                var cmd = new TestableCreateCuratedPackageCommand();
                cmd.StubPackageRegistration.Key = 1066;

                var curatedPackage = cmd.Execute(
                    42,
                    1066,
                    false,
                    true,
                    "theNotes");

                Assert.Equal(1066, curatedPackage.PackageRegistrationKey);
                Assert.Equal(false, curatedPackage.Included);
                Assert.Equal(true, curatedPackage.AutomaticallyCurated);
                Assert.Equal("theNotes", curatedPackage.Notes);

            }
        }

        public class TestableCreateCuratedPackageCommand : CreateCuratedPackageCommand
        {
            public TestableCreateCuratedPackageCommand()
                :  base(null)
            {
                StubCuratedFeed = new CuratedFeed { Key = 0, Name = "aName", };
                StubCuratedFeedByKeyQry = new Mock<ICuratedFeedByKeyQuery>();
                StubEntitiesContext = new Mock<IEntitiesContext>();
                StubPackageRegistration = new PackageRegistration { Key = 0, };
                StubPackageRegistrationByKeyQry = new Mock<IPackageRegistrationByKeyQuery>();

                StubCuratedFeedByKeyQry
                   .Setup(stub => stub.Execute(It.IsAny<int>(), It.IsAny<bool>()))
                   .Returns(StubCuratedFeed);
                StubPackageRegistrationByKeyQry
                    .Setup(stub => stub.Execute(It.IsAny<int>(), It.IsAny<bool>()))
                    .Returns(StubPackageRegistration);
                
                Entities = StubEntitiesContext.Object;
            }

            public CuratedFeed StubCuratedFeed{ get; set; }
            public Mock<ICuratedFeedByKeyQuery> StubCuratedFeedByKeyQry { get; set; }
            public Mock<IEntitiesContext> StubEntitiesContext { get; private set; }
            public PackageRegistration StubPackageRegistration { get; set; }
            public Mock<IPackageRegistrationByKeyQuery> StubPackageRegistrationByKeyQry { get; set; }

            protected override T GetService<T>()
            {
                if (typeof(T) == typeof(ICuratedFeedByKeyQuery))
                    return (T) StubCuratedFeedByKeyQry.Object;

                if (typeof(T) == typeof(IPackageRegistrationByKeyQuery))
                    return (T) StubPackageRegistrationByKeyQry.Object;

                throw new Exception("Tried to get unexpected service");
            }
        }
    }
}
