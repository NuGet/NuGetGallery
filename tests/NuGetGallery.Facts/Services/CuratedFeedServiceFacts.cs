// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    class CuratedFeedServiceFacts
    {
        public class TestableCuratedFeedService : CuratedFeedService
        {
            public TestableCuratedFeedService()
            {
                StubCuratedFeed = new CuratedFeed { Key = 0, Name = "aName" };
                StubPackageRegistration = new PackageRegistration { Key = 1066, Id = "aPackageId" };

                StubCuratedPackage = new CuratedPackage
                {
                    Key = 0,
                    CuratedFeedKey = StubCuratedFeed.Key,
                    CuratedFeed = StubCuratedFeed,
                    PackageRegistration = StubPackageRegistration,
                    PackageRegistrationKey = StubPackageRegistration.Key
                };
                StubCuratedFeed.Packages.Add(StubCuratedPackage);

                StubCuratedFeedRepository = new Mock<IEntityRepository<CuratedFeed>>();
                StubCuratedFeedRepository
                    .Setup(repo => repo.GetAll())
                    .Returns(new CuratedFeed[] { StubCuratedFeed }.AsQueryable());

                StubCuratedPackageRepository = new Mock<IEntityRepository<CuratedPackage>>();
                StubCuratedPackageRepository
                    .Setup(repo => repo.GetAll())
                    .Returns(new CuratedPackage[] { StubCuratedPackage }.AsQueryable());
            }

            public Mock<IEntityRepository<CuratedFeed>> StubCuratedFeedRepository {
                get
                {
                    return _stubCuratedFeedRepository;
                }
                set
                {
                    _stubCuratedFeedRepository = value;
                    CuratedFeedRepository = value.Object;
                }
            }

            public Mock<IEntityRepository<CuratedPackage>> StubCuratedPackageRepository {
                get
                {
                    return _stubCuratedPackageRepository;
                }
                set
                {
                    _stubCuratedPackageRepository = value;
                    CuratedPackageRepository = value.Object;
                }
            }

            public PackageRegistration StubPackageRegistration { get; set; }
            public CuratedFeed StubCuratedFeed { get; set; }
            public CuratedPackage StubCuratedPackage { get; set; }

            Mock<IEntityRepository<CuratedFeed>> _stubCuratedFeedRepository;
            Mock<IEntityRepository<CuratedPackage>> _stubCuratedPackageRepository;
        }

        public class TheCreateCuratedPackageMethod
        {
            [Fact]
            public async Task WillThrowWhenCuratedFeedDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                await Assert.ThrowsAsync<ArgumentNullException>(
                    async () => await svc.CreatedCuratedPackageAsync(
                        null,
                        svc.StubPackageRegistration));
            }

            [Fact]
            public async Task WillThrowWhenPackageRegistrationDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                await Assert.ThrowsAsync<ArgumentNullException>(
                    async () => await svc.CreatedCuratedPackageAsync(
                        svc.StubCuratedFeed,
                        null));
            }

            [Fact]
            public async Task WillAddANewCuratedPackageToTheCuratedFeed()
            {
                var svc = new TestableCuratedFeedService();
                svc.StubPackageRegistration.Key = 1066;

                await svc.CreatedCuratedPackageAsync(
                    svc.StubCuratedFeed,
                    svc.StubPackageRegistration,
                    false,
                    true,
                    "theNotes");

                var curatedPackage = svc.StubCuratedFeed.Packages.First();
                Assert.Equal(1066, curatedPackage.PackageRegistrationKey);
                Assert.False(curatedPackage.Included);
                Assert.True(curatedPackage.AutomaticallyCurated);
                Assert.Equal("theNotes", curatedPackage.Notes);
            }

            [Fact]
            public async Task WillSaveTheEntityChanges()
            {
                var svc = new TestableCuratedFeedService();

                await svc.CreatedCuratedPackageAsync(
                    svc.StubCuratedFeed,
                    svc.StubPackageRegistration,
                    false,
                    true,
                    "theNotes");

                svc.StubCuratedPackageRepository.Verify(stub => stub.InsertOnCommit(It.IsAny<CuratedPackage>()));
                svc.StubCuratedPackageRepository.Verify(stub => stub.CommitChangesAsync());
            }

            [Fact]
            public async Task WillReturnTheCreatedCuratedPackage()
            {
                var svc = new TestableCuratedFeedService();
                svc.StubPackageRegistration.Key = 1066;

                var curatedPackage = await svc.CreatedCuratedPackageAsync(
                    svc.StubCuratedFeed,
                    svc.StubPackageRegistration,
                    false,
                    true,
                    "theNotes");

                Assert.Equal(1066, curatedPackage.PackageRegistrationKey);
                Assert.False(curatedPackage.Included);
                Assert.True(curatedPackage.AutomaticallyCurated);
                Assert.Equal("theNotes", curatedPackage.Notes);
            }
        }


        public class TheModifyCuratedPackageMethod
        {
            [Fact]
            public async Task WillThrowWhenCuratedFeedDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await svc.ModifyCuratedPackageAsync(
                        42,
                        0,
                        false));
            }

            [Fact]
            public async Task WillThrowWhenCuratedPackageDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await svc.ModifyCuratedPackageAsync(
                        0,
                        404,
                        false));
            }

            [Fact]
            public async Task WillModifyAndSaveTheCuratedPackage()
            {
                var svc = new TestableCuratedFeedService();

                await svc.ModifyCuratedPackageAsync(
                    0,
                    1066,
                    true);

                Assert.True(svc.StubCuratedPackage.Included);
                svc.StubCuratedPackageRepository.Verify(stub => stub.CommitChangesAsync());
            }
        }

        public class TheDeleteCuratedPackageMethod
        {
            [Fact]
            public async Task WillThrowWhenCuratedFeedDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await svc.DeleteCuratedPackageAsync(
                        42,
                        0));
            }

            [Fact]
            public async Task WillThrowWhenCuratedPackageDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await svc.DeleteCuratedPackageAsync(
                        0,
                        1066));
            }

            [Fact]
            public async Task WillDeleteTheCuratedPackage()
            {
                var svc = new TestableCuratedFeedService();

                await svc.DeleteCuratedPackageAsync(
                    0,
                    1066);

                svc.StubCuratedPackageRepository.Verify(stub => stub.DeleteOnCommit(svc.StubCuratedPackage));
                svc.StubCuratedPackageRepository.Verify(stub => stub.CommitChangesAsync());
            }
        }
    }
}
