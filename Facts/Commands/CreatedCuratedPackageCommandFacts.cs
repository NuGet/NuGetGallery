using System;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class CreatedCuratedPackageCommandFacts
    {
        public class TestableCreateCuratedPackageCommand : CreateCuratedPackageCommand
        {
            public TestableCreateCuratedPackageCommand()
                : base(null)
            {
                StubCuratedFeed = new CuratedFeed { Key = 0, Name = "aName", };
                StubEntitiesContext = new Mock<IEntitiesContext>();
                StubPackageRegistration = new PackageRegistration { Key = 0, };
                Entities = StubEntitiesContext.Object;
            }

            public CuratedFeed StubCuratedFeed { get; set; }
            public Mock<IEntitiesContext> StubEntitiesContext { get; private set; }
            public PackageRegistration StubPackageRegistration { get; set; }
        }

        public class TheExecuteMethod
        {
            [Fact]
            public void WillThrowWhenCuratedFeedDoesNotExist()
            {
                var cmd = new TestableCreateCuratedPackageCommand();

                Assert.Throws<ArgumentNullException>(
                    () => cmd.Execute(
                        null,
                        cmd.StubPackageRegistration));
            }

            [Fact]
            public void WillThrowWhenPackageRegistrationDoesNotExist()
            {
                var cmd = new TestableCreateCuratedPackageCommand();

                Assert.Throws<ArgumentNullException>(
                    () => cmd.Execute(
                        cmd.StubCuratedFeed,
                        null));
            }

            [Fact]
            public void WillAddANewCuratedPackageToTheCuratedFeed()
            {
                var cmd = new TestableCreateCuratedPackageCommand();
                cmd.StubPackageRegistration.Key = 1066;

                cmd.Execute(
                    cmd.StubCuratedFeed,
                    cmd.StubPackageRegistration,
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
                    cmd.StubCuratedFeed,
                    cmd.StubPackageRegistration,
                    false,
                    true,
                    "theNotes");

                cmd.StubEntitiesContext.Verify(stub => stub.SaveChanges());
            }

            [Fact]
            public void WillReturnTheCreatedCuratedPackage()
            {
                var cmd = new TestableCreateCuratedPackageCommand();
                cmd.StubPackageRegistration.Key = 1066;

                var curatedPackage = cmd.Execute(
                    cmd.StubCuratedFeed,
                    cmd.StubPackageRegistration,
                    false,
                    true,
                    "theNotes");

                Assert.Equal(1066, curatedPackage.PackageRegistrationKey);
                Assert.Equal(false, curatedPackage.Included);
                Assert.Equal(true, curatedPackage.AutomaticallyCurated);
                Assert.Equal("theNotes", curatedPackage.Notes);
            }
        }
    }
}