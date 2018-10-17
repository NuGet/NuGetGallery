// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class CuratedFeedServiceFacts
    {
        public class TheGetFeedByNameMethod : Facts
        {
            [Fact]
            public void ReturnsNullWhenFeedIsDisabled()
            {
                DisableFeed(_curatedFeed.Name);

                var output = _target.GetFeedByName(_curatedFeed.Name);

                Assert.Null(output);
                _entityRepository.Verify(x => x.GetAll(), Times.Never);
            }

            [Fact]
            public void ReturnsNullWhenFeedIsDisabledWithDifferentCasing()
            {
                _curatedFeed.Name = "curated-feed";
                DisableFeed("CURATED-Feed");

                var output = _target.GetFeedByName(_curatedFeed.Name);

                Assert.Null(output);
                _entityRepository.Verify(x => x.GetAll(), Times.Never);
            }

            [Fact]
            public void ReturnsFeedWhenNameMatchesAndFeedIsNotDisabled()
            {
                var output = _target.GetFeedByName(_curatedFeed.Name);

                Assert.Same(_curatedFeed, output);
                _entityRepository.Verify(x => x.GetAll(), Times.Once);

            }

            [Fact]
            public void ReturnsNullWhenFeedDoesNotExist()
            {
                var output = _target.GetFeedByName("something-else");

                Assert.Null(output);
                _entityRepository.Verify(x => x.GetAll(), Times.Once);
            }
        }

        public class TheGetPackagesMethod : Facts
        {
            [Fact]
            public void ReturnsEmptyListWhenFeedIsDisabled()
            {
                DisableFeed(_curatedFeed.Name);

                var output = _target.GetPackages(_curatedFeed.Name);

                Assert.Empty(output);
                _entityRepository.Verify(x => x.GetAll(), Times.Never);
            }

            [Fact]
            public void ReturnsPackagesWhenNameMatchesAndFeedIsNotDisabled()
            {
                var output = _target.GetPackages(_curatedFeed.Name);

                var package = Assert.Single(output);
                Assert.Same(_package, package);

            }

            [Fact]
            public void ReturnsNullWhenFeedDoesNotExist()
            {
                var output = _target.GetPackages("something-else");

                Assert.Empty(output);
                _entityRepository.Verify(x => x.GetAll(), Times.Once);
            }
        }

        public abstract class Facts
        {
            protected Package _package;
            protected CuratedFeed _curatedFeed;
            protected readonly Mock<IEntityRepository<CuratedFeed>> _entityRepository;
            protected readonly Mock<IAppConfiguration> _config;
            protected readonly CuratedFeedService _target;

            protected Facts()
            {
                _package = new Package();
                _curatedFeed = new CuratedFeed
                {
                    Name = "curated-feed",
                    Packages = new[]
                    {
                        new CuratedPackage
                        {
                            PackageRegistration = new PackageRegistration
                            {
                                Packages = new[]
                                {
                                    _package,
                                },
                            },
                        },
                    },
                };

                _entityRepository = new Mock<IEntityRepository<CuratedFeed>>();
                _config = new Mock<IAppConfiguration>();

                _entityRepository
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _curatedFeed }.AsQueryable());

                _target = new CuratedFeedService(
                    _entityRepository.Object,
                    _config.Object);
            }

            protected void DisableFeed(string feedName)
            {
                _config
                    .Setup(x => x.DisabledCuratedFeeds)
                    .Returns(new[] { feedName });
            }
        }
    }
}
