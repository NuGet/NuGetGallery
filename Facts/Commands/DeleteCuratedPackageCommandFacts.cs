using System;
using Moq;
using Xunit;
using System.Data.Entity;

namespace NuGetGallery.Commands
{
    public class DeleteCuratedPackageCommandFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public void WillThrowWhenCuratedFeedDoesNotExist()
            {
                var cmd = new TestableDeleteCuratedPackageCommand();
                cmd.StubCuratedFeedByKeyQry
                    .Setup(stub => stub.Execute(It.IsAny<int>(), It.IsAny<bool>()))
                    .Returns((CuratedFeed)null);

                Assert.Throws<InvalidOperationException>(() => cmd.Execute(
                    42,
                    0));
            }

            [Fact]
            public void WillThrowWhenCuratedPackageDoesNotExist()
            {
                var cmd = new TestableDeleteCuratedPackageCommand();
                cmd.StubCuratedFeed.Packages = new[] { new CuratedPackage() { Key = 0 } };

                Assert.Throws<InvalidOperationException>(() => cmd.Execute(
                    0,
                    1066));
            }

            [Fact]
            public void WillDeleteTheCuratedPackage()
            {
                var cmd = new TestableDeleteCuratedPackageCommand();
                var stubCuratedPackage = new CuratedPackage { Key = 1066 };
                cmd.StubCuratedFeed.Packages = new[] { stubCuratedPackage };

                cmd.Execute(
                    0,
                    1066);

                cmd.StubCuratedPackageDbSet.Verify(stub => stub.Remove(stubCuratedPackage));
                cmd.StubEntitiesContext.Verify(stub => stub.SaveChanges());
            }
        }
        
        public class TestableDeleteCuratedPackageCommand : DeleteCuratedPackageCommand
        {
            public TestableDeleteCuratedPackageCommand()
                : base(null)
            {
                StubCuratedFeed = new CuratedFeed { Key = 0, Name = "aName", };
                StubCuratedPackageDbSet = new Mock<IDbSet<CuratedPackage>>();
                StubCuratedFeedByKeyQry = new Mock<ICuratedFeedByKeyQuery>();
                StubEntitiesContext = new Mock<IEntitiesContext>();

                StubCuratedFeedByKeyQry
                   .Setup(stub => stub.Execute(It.IsAny<int>(), It.IsAny<bool>()))
                   .Returns(StubCuratedFeed);
                StubEntitiesContext
                    .Setup(stub => stub.CuratedPackages)
                    .Returns(StubCuratedPackageDbSet.Object);

                Entities = StubEntitiesContext.Object;
            }

            public CuratedFeed StubCuratedFeed { get; set; }
            public Mock<IDbSet<CuratedPackage>> StubCuratedPackageDbSet { get; private set; }
            public Mock<ICuratedFeedByKeyQuery> StubCuratedFeedByKeyQry { get; set; }
            public Mock<IEntitiesContext> StubEntitiesContext { get; private set; }

            protected override T GetService<T>()
            {
                if (typeof(T) == typeof(ICuratedFeedByKeyQuery))
                    return (T)StubCuratedFeedByKeyQry.Object;

                throw new Exception("Tried to get unexpected service");
            }
        }
    }
}
