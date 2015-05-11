// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                : base()
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
            public void WillThrowWhenCuratedFeedDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                Assert.Throws<ArgumentNullException>(
                    () => svc.CreatedCuratedPackage(
                        null,
                        svc.StubPackageRegistration));
            }

            [Fact]
            public void WillThrowWhenPackageRegistrationDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                Assert.Throws<ArgumentNullException>(
                    () => svc.CreatedCuratedPackage(
                        svc.StubCuratedFeed,
                        null));
            }

            [Fact]
            public void WillAddANewCuratedPackageToTheCuratedFeed()
            {
                var svc = new TestableCuratedFeedService();
                svc.StubPackageRegistration.Key = 1066;

                svc.CreatedCuratedPackage(
                    svc.StubCuratedFeed,
                    svc.StubPackageRegistration,
                    false,
                    true,
                    "theNotes");

                var curatedPackage = svc.StubCuratedFeed.Packages.First();
                Assert.Equal(1066, curatedPackage.PackageRegistrationKey);
                Assert.Equal(false, curatedPackage.Included);
                Assert.Equal(true, curatedPackage.AutomaticallyCurated);
                Assert.Equal("theNotes", curatedPackage.Notes);
            }

            [Fact]
            public void WillSaveTheEntityChanges()
            {
                var svc = new TestableCuratedFeedService();

                svc.CreatedCuratedPackage(
                    svc.StubCuratedFeed,
                    svc.StubPackageRegistration,
                    false,
                    true,
                    "theNotes");

                svc.StubCuratedPackageRepository.Verify(stub => stub.InsertOnCommit(It.IsAny<CuratedPackage>()));
                svc.StubCuratedPackageRepository.Verify(stub => stub.CommitChanges());
            }

            [Fact]
            public void WillReturnTheCreatedCuratedPackage()
            {
                var svc = new TestableCuratedFeedService();
                svc.StubPackageRegistration.Key = 1066;

                var curatedPackage = svc.CreatedCuratedPackage(
                    svc.StubCuratedFeed,
                    svc.StubPackageRegistration,
                    false,
                    true,
                    "theNotes");

                Assert.Equal(1066, curatedPackage.PackageRegistrationKey);
                Assert.Equal(false, curatedPackage.Included);
                Assert.Equal(true, curatedPackage.AutomaticallyCurated);
                Assert.Equal("theNotes", curatedPackage.Notes);
            }
        }


        public class TheModifyCuratedPackageMethod
        {
            [Fact]
            public void WillThrowWhenCuratedFeedDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                Assert.Throws<InvalidOperationException>(
                    () => svc.ModifyCuratedPackage(
                        42,
                        0,
                        false));
            }

            [Fact]
            public void WillThrowWhenCuratedPackageDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                Assert.Throws<InvalidOperationException>(
                    () => svc.ModifyCuratedPackage(
                        0,
                        404,
                        false));
            }

            [Fact]
            public void WillModifyAndSaveTheCuratedPackage()
            {
                var svc = new TestableCuratedFeedService();

                svc.ModifyCuratedPackage(
                    0,
                    1066,
                    true);

                Assert.True(svc.StubCuratedPackage.Included);
                svc.StubCuratedPackageRepository.Verify(stub => stub.CommitChanges());
            }
        }

        public class TheDeleteCuratedPackageMethod
        {
            [Fact]
            public void WillThrowWhenCuratedFeedDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                Assert.Throws<InvalidOperationException>(
                    () => svc.DeleteCuratedPackage(
                        42,
                        0));
            }

            [Fact]
            public void WillThrowWhenCuratedPackageDoesNotExist()
            {
                var svc = new TestableCuratedFeedService();

                Assert.Throws<InvalidOperationException>(
                    () => svc.DeleteCuratedPackage(
                        0,
                        1066));
            }

            [Fact]
            public void WillDeleteTheCuratedPackage()
            {
                var svc = new TestableCuratedFeedService();

                svc.DeleteCuratedPackage(
                    0,
                    1066);

                svc.StubCuratedPackageRepository.Verify(stub => stub.DeleteOnCommit(svc.StubCuratedPackage));
                svc.StubCuratedPackageRepository.Verify(stub => stub.CommitChanges());
            }
        }
    }
}
