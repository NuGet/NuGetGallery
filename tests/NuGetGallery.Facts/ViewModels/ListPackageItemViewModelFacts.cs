// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class ListPackageItemViewModelFacts
    {
        // start with replicating the PackageViewModelFacts here since we shouldn't be breaking these
        // ListPackageItemViewModel extends PackageViewModel
        #region CopiedFromPackageViewModelFacts
        [Fact]
        public void UsesNormalizedVersionForDisplay()
        {
            var package = new Package()
            {
                Version = "01.02.00.00",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                NormalizedVersion = "1.3.0" // Different just to prove the View Model is using the DB column.
            };
            var packageViewModel = CreateListPackageItemViewModel(package);
            Assert.Equal("1.3.0", packageViewModel.Version);
        }

        [Fact]
        public void UsesNormalizedPackageVersionIfNormalizedVersionMissing()
        {
            var package = new Package()
            {
                Version = "01.02.00.00",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
            };
            var packageViewModel = CreateListPackageItemViewModel(package);
            Assert.Equal("1.2.0", packageViewModel.Version);
        }

        #endregion

        [Fact]
        public void ShortDescriptionsNotTruncated()
        {
            var description = "A Short Description";
            var package = new Package()
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                Description = description
            };

            var listPackageItemViewModel = CreateListPackageItemViewModel(package);

            Assert.Equal(description, listPackageItemViewModel.ShortDescription);
            Assert.False(listPackageItemViewModel.IsDescriptionTruncated);
        }

        [Fact]
        public void LongDescriptionsTruncated()
        {
            var omission = "...";
            var description = @"A Longer description full of nonsense that will get truncated. Lorem ipsum dolor sit amet, ad nemore gubergren eam. Ea quaeque labores deseruisse his, eos munere convenire at, in eos audire persius corpora. Te his volumus detracto offendit, has ne illud choro. No illum quaestio mel, novum democritum te sea, et nam nisl officiis salutandi. Vis ut harum docendi incorrupte, nam affert putent sententiae id, mei cibo omnium id. Ea est falli graeci voluptatibus, est mollis denique ne.
An nec tempor cetero vituperata.Ius cu dicunt regione interpretaris, posse veniam facilisis ad vim, sit ei sale integre. Mel cu aliquid impedit scribentur.Nostro recusabo sea ei, nec habeo instructior no, saepe altera adversarium vel cu.Nonumes molestiae sit at, per enim necessitatibus cu.
At mei iriure dignissim theophrastus.Meis nostrud te sit, equidem maiorum pri ex.Vim dolorem fuisset an. At sit veri illum oratio, et per dicat contentiones. In eam tale tation, mei dicta labitur corpora ei, homero equidem suscipit ut eam.";

            var package = new Package()
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                Description = description
            };

            var listPackageItemViewModel = CreateListPackageItemViewModel(package);

            Assert.NotEqual(description, listPackageItemViewModel.ShortDescription);
            Assert.True(listPackageItemViewModel.IsDescriptionTruncated);
            Assert.EndsWith(omission, listPackageItemViewModel.ShortDescription);
            Assert.Contains(listPackageItemViewModel.ShortDescription.Substring(0, listPackageItemViewModel.ShortDescription.Length - 1 - omission.Length), description);
        }

        [Fact]
        public void LongDescriptionsSingleWordTruncatedToLimit()
        {
            var charLimit = 300;
            var omission = "...";
            var description = @"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz";

            var package = new Package()
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                Description = description
            };

            var listPackageItemViewModel = CreateListPackageItemViewModel(package);

            Assert.Equal(charLimit + omission.Length, listPackageItemViewModel.ShortDescription.Length);
            Assert.True(listPackageItemViewModel.IsDescriptionTruncated);
            Assert.EndsWith(omission, listPackageItemViewModel.ShortDescription);
        }

        [Fact]
        public void EmptyTagsAreParsedEmpty()
        {
            var package = new Package()
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
            };

            var listPackageItemViewModel = CreateListPackageItemViewModel(package);

            Assert.Null(listPackageItemViewModel.Tags);
        }

        [Fact]
        public void TagsAreParsed()
        {
            var package = new Package()
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                Tags = "tag1 tag2 tag3"
            };

            var listPackageItemViewModel = CreateListPackageItemViewModel(package);

            Assert.Equal(3, listPackageItemViewModel.Tags.Count());
            Assert.Contains("tag1", listPackageItemViewModel.Tags);
            Assert.Contains("tag2", listPackageItemViewModel.Tags);
            Assert.Contains("tag3", listPackageItemViewModel.Tags);
        }

        [Fact]
        public void AuthorsIsFlattenedAuthors()
        {
            var authors = new HashSet<PackageAuthor>();
            var author1 = new PackageAuthor
            {
                Name = "author1"
            };
            var author2 = new PackageAuthor
            {
                Name = "author2"
            };

            authors.Add(author1);
            authors.Add(author2);

            var flattenedAuthors = "something Completely different";

            var package = new Package()
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
#pragma warning disable 0618
                Authors = authors,
#pragma warning restore 0618
                FlattenedAuthors = flattenedAuthors
            };

            var listPackageItemViewModel = CreateListPackageItemViewModel(package);

            Assert.Equal(flattenedAuthors, listPackageItemViewModel.Authors);
        }

        [Fact]
        public void UseVersionIfLatestSemVer2AndStableSemVer2NotSame()
        {
            var package = new Package()
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                SemVerLevelKey = SemVerLevelKey.SemVer2,
                IsLatestSemVer2 = true,
                IsLatestStableSemVer2 = false
            };

            var listPackageItemViewModel = CreateListPackageItemViewModel(package);
            Assert.True(listPackageItemViewModel.UseVersion);

            listPackageItemViewModel.LatestVersionSemVer2 = false;
            listPackageItemViewModel.LatestStableVersionSemVer2 = true;
            Assert.True(listPackageItemViewModel.UseVersion);

            listPackageItemViewModel.LatestVersionSemVer2 = false;
            listPackageItemViewModel.LatestStableVersionSemVer2 = false;
            Assert.True(listPackageItemViewModel.UseVersion);

            listPackageItemViewModel.LatestVersionSemVer2 = true;
            listPackageItemViewModel.LatestStableVersionSemVer2 = true;
            Assert.False(listPackageItemViewModel.UseVersion);
        }
        
        public class SignerInformation
        {
            private readonly User _user1;
            private readonly User _user2;
            private readonly User _user3;
            private readonly Certificate _certificate;
            private readonly PackageRegistration _packageRegistration;
            private readonly Package _package;

            public SignerInformation()
            {
                _user1 = new User()
                {
                    Key = 1,
                    Username = "A"
                };
                _user2 = new User()
                {
                    Key = 2,
                    Username = "B"
                };
                _user3 = new User()
                {
                    Key = 3,
                    Username = "C"
                };

                _certificate = new Certificate()
                {
                    Key = 4,
                    Thumbprint = "D",
                };

                _packageRegistration = new PackageRegistration()
                {
                    Key = 5,
                    Id = "F"
                };

                _package = new Package()
                {
                    Key = 6,
                    Version = "1.0.0",
                    PackageRegistration = _packageRegistration
                };

                _packageRegistration.Packages.Add(_package);
            }

            [Fact]
            public void WhenCannotDisplayPrivateMetadata_ReturnsNull()
            {
                var viewModel = CreateListPackageItemViewModel(_package, _user1);

                Assert.False(viewModel.CanDisplayPrivateMetadata);
                Assert.Null(viewModel.SignatureInformation);
            }

            [Fact]
            public void WhenPackageCertificateIsNull_ReturnsNull()
            {
                _packageRegistration.Owners.Add(_user1);

                var viewModel = CreateListPackageItemViewModel(_package, _user1);

                Assert.True(viewModel.CanDisplayPrivateMetadata);
                Assert.Null(viewModel.SignatureInformation);
            }

            [Fact]
            public void WhenPackageCertificateIsNotNullAndNoOwners_ReturnsString()
            {
                SignPackage();

                var viewModel = CreateListPackageItemViewModel(_package, _user1);

                viewModel.CanDisplayPrivateMetadata = true;

                Assert.Equal("Signed with certificate (D)", viewModel.SignatureInformation);
            }

            [Fact]
            public void WhenPackageCertificateIsNotNullAndOneSigner_ReturnsString()
            {
                _packageRegistration.Owners.Add(_user1);

                ActivateCertificate(_user1);
                SignPackage();

                var viewModel = CreateListPackageItemViewModel(_package, _user1);

                Assert.True(viewModel.CanDisplayPrivateMetadata);
                Assert.Equal("Signed with A's certificate (D)", viewModel.SignatureInformation);
            }

            [Fact]
            public void WhenPackageCertificateIsNotNullAndTwoSigners_ReturnsString()
            {
                _packageRegistration.Owners.Add(_user1);
                _packageRegistration.Owners.Add(_user2);

                ActivateCertificate(_user1);
                ActivateCertificate(_user2);
                SignPackage();

                var viewModel = CreateListPackageItemViewModel(_package, _user1);

                Assert.True(viewModel.CanDisplayPrivateMetadata);
                Assert.Equal("Signed with A and B's certificate (D)", viewModel.SignatureInformation);
            }

            [Fact]
            public void WhenPackageCertificateIsNotNullAndThreeSigners_ReturnsString()
            {
                _packageRegistration.Owners.Add(_user1);
                _packageRegistration.Owners.Add(_user2);
                _packageRegistration.Owners.Add(_user3);

                ActivateCertificate(_user1);
                ActivateCertificate(_user2);
                ActivateCertificate(_user3);
                SignPackage();

                var viewModel = CreateListPackageItemViewModel(_package, _user1);

                Assert.True(viewModel.CanDisplayPrivateMetadata);
                Assert.Equal("Signed with A, B, and C's certificate (D)", viewModel.SignatureInformation);
            }

            private void ActivateCertificate(User user)
            {
                var userCertificate = new UserCertificate()
                {
                    Key = _certificate.UserCertificates.Count() + 1,
                    UserKey = user.Key,
                    User = user,
                    CertificateKey = _certificate.Key,
                    Certificate = _certificate
                };

                _certificate.UserCertificates.Add(userCertificate);
                user.UserCertificates.Add(userCertificate);
            }

            private void SignPackage()
            {
                _package.CertificateKey = _certificate.Key;
                _package.Certificate = _certificate;
            }
        }

        private static ListPackageItemViewModel CreateListPackageItemViewModel(Package package, User user = null)
        {
            return new ListPackageItemViewModelFactory(Mock.Of<IIconUrlProvider>()).Create(package, currentUser: user);
        }
    }
}