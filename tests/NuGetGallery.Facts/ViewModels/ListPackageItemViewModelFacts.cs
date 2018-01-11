using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                NormalizedVersion = "1.3.0" // Different just to prove the View Model is using the DB column.
            };
            var packageViewModel = new ListPackageItemViewModel(package, currentUser: null);
            Assert.Equal("1.3.0", packageViewModel.Version);
        }

        [Fact]
        public void UsesNormalizedPackageVersionIfNormalizedVersionMissing()
        {
            var package = new Package()
            {
                Version = "01.02.00.00"
            };
            var packageViewModel = new ListPackageItemViewModel(package, currentUser: null);
            Assert.Equal("1.2.0", packageViewModel.Version);
        }

        [Fact]
        public void LicenseNamesAreParsedByCommas()
        {
            var package = new Package
            {
                Version = "1.0.0",
                LicenseNames = "l1,l2, l3 ,l4  ,  l5 ",
            };
            var packageViewModel = new ListPackageItemViewModel(package, currentUser: null);
            Assert.Equal(new string[] { "l1", "l2", "l3", "l4", "l5" }, packageViewModel.LicenseNames);
        }

        [Fact]
        public void LicenseReportFieldsKeptWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = true,
                LicenseNames = "l1",
                LicenseReportUrl = "url"
            };
            var packageViewModel = new ListPackageItemViewModel(package, currentUser: null);
            Assert.NotNull(packageViewModel.LicenseNames);
            Assert.NotNull(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseReportUrlKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = false,
                LicenseReportUrl = "url"
            };
            var packageViewModel = new ListPackageItemViewModel(package, currentUser: null);
            Assert.NotNull(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseNamesKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = false,
                LicenseNames = "l1"
            };
            var packageViewModel = new ListPackageItemViewModel(package, currentUser: null);
            Assert.NotNull(packageViewModel.LicenseNames);
        }

        [Fact]
        public void LicenseUrlKeptWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = true,
                LicenseUrl = "url"
            };
            var packageViewModel = new ListPackageItemViewModel(package, currentUser: null);
            Assert.NotNull(packageViewModel.LicenseUrl);
        }
        #endregion

        [Fact]
        public void ShortDescriptionsNotTruncated()
        {
            var description = "A Short Description";
            var package = new Package()
            {
                Version = "1.0.0",
                Description = description
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);

            Assert.Equal(description, listPackageItemViewModel.ShortDescription);
            Assert.Equal(false, listPackageItemViewModel.IsDescriptionTruncated);
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
                Description = description
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);

            Assert.NotEqual(description, listPackageItemViewModel.ShortDescription);
            Assert.Equal(true, listPackageItemViewModel.IsDescriptionTruncated);
            Assert.True(listPackageItemViewModel.ShortDescription.EndsWith(omission));
            Assert.True(description.Contains(listPackageItemViewModel.ShortDescription.Substring(0, listPackageItemViewModel.ShortDescription.Length - 1 - omission.Length)));
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
                Description = description
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);

            Assert.Equal(charLimit + omission.Length, listPackageItemViewModel.ShortDescription.Length);
            Assert.Equal(true, listPackageItemViewModel.IsDescriptionTruncated);
            Assert.True(listPackageItemViewModel.ShortDescription.EndsWith(omission));
        }

        [Fact]
        public void EmptyTagsAreParsedEmpty()
        {
            var package = new Package()
            {
                Version = "1.0.0"
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);

            Assert.Equal(null, listPackageItemViewModel.Tags);
        }

        [Fact]
        public void TagsAreParsed()
        {
            var package = new Package()
            {
                Version = "1.0.0",
                Tags = "tag1 tag2 tag3"
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);

            Assert.Equal(3, listPackageItemViewModel.Tags.Count());
            Assert.True(listPackageItemViewModel.Tags.Contains("tag1"));
            Assert.True(listPackageItemViewModel.Tags.Contains("tag2"));
            Assert.True(listPackageItemViewModel.Tags.Contains("tag3"));
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
#pragma warning disable 0618
                Authors = authors,
#pragma warning restore 0618
                FlattenedAuthors = flattenedAuthors
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);

            Assert.Equal(flattenedAuthors, listPackageItemViewModel.Authors);
        }

        [Fact]
        public void UseVersionIfLatestAndStableNotSame()
        {
            var package = new Package()
            {
                Version = "1.0.0",
                IsLatest = true,
                IsLatestStable = false
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);
            Assert.True(listPackageItemViewModel.UseVersion);

            listPackageItemViewModel.LatestVersion = false;
            listPackageItemViewModel.LatestStableVersion = true;
            Assert.True(listPackageItemViewModel.UseVersion);

            listPackageItemViewModel.LatestVersion = false;
            listPackageItemViewModel.LatestStableVersion = false;
            Assert.True(listPackageItemViewModel.UseVersion);

            listPackageItemViewModel.LatestVersion = true;
            listPackageItemViewModel.LatestStableVersion = true;
            Assert.False(listPackageItemViewModel.UseVersion);
        }

        [Fact]
        public void UseVersionIfLatestSemVer2AndStableSemVer2NotSame()
        {
            var package = new Package()
            {
                Version = "1.0.0",
                SemVerLevelKey = SemVerLevelKey.SemVer2,
                IsLatestSemVer2 = true,
                IsLatestStableSemVer2 = false
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);
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

        [Theory]
        [MemberData(nameof(HasSingleOwner_Input))]
        public void HasSingleOwner(Package package, bool expectedResult)
        {
            // Arrange
            var listPackageItemViewModel = new ListPackageItemViewModel(package, currentUser: null);

            // Act + Assert
            Assert.Equal(expectedResult, listPackageItemViewModel.HasSingleOwner);
        }

        public static IEnumerable<object[]> HasSingleOwner_Input
        {
            get
            {
                List<object[]> result = new List<object[]>();

                var description = "Description";
                var packageRegistration0 = CreatePackageRegistration(0);
                var result0 = false;
                result.Add(new object[] { new Package() { Key = 0, Version = "1.0.0", PackageRegistration = packageRegistration0, Description = description }, result0 });

                var packageRegistration1 = CreatePackageRegistration(1);
                packageRegistration1.Owners.Add(new User() { Username = "user1", Key = 1 });
                var result1 = true;
                result.Add(new object[] { new Package() { Key = 1, Version = "1.0.0", PackageRegistration = packageRegistration1, Description = description }, result1 });

                var packageRegistration2 = CreatePackageRegistration(2);
                packageRegistration2.Owners.Add(new User() { Username = "user2.1", Key = 1 });
                packageRegistration2.Owners.Add(new User() { Username = "user2.2", Key = 2});
                var result2 = false;
                result.Add(new object[] { new Package() { Key = 2, Version = "1.0.0", PackageRegistration = packageRegistration2, Description = description }, result2 });

                var packageRegistration3 = CreatePackageRegistration(3);
                packageRegistration3.Owners.Add(new Organization() { Username = "userOrg3", Members = new List<Membership>() });
                var result3 = false;
                result.Add(new object[] { new Package() { Key = 3, Version = "1.0.0", PackageRegistration = packageRegistration3, Description = description }, result3 });

                var packageRegistration4 = CreatePackageRegistration(4);
                packageRegistration4.Owners.Add(new User() { Username = "user4.1" });
                packageRegistration4.Owners.Add(new Organization() { Username = "userOrg4" });
                var result4 = true;
                result.Add(new object[] { new Package() { Key = 4, Version = "1.0.0", PackageRegistration = packageRegistration4, Description = description }, result4 });

                // A single organization with one owner
                var packageRegistration5 = CreatePackageRegistration(5);
                var user51 = new User() { Username = "user5.1", Key = 51 };
                packageRegistration5.Owners.Add(new Organization() {
                                                        Username = "userOrg5",
                                                        Key = 50,
                                                        Members = new List<Membership>
                                                        {
                                                            new Membership(){
                                                                Member = user51,
                                                                MemberKey = user51.Key,
                                                                OrganizationKey = 50
                                                            }
                                                        } });
                var result5 = true;
                result.Add(new object[] { new Package() { Key = 5, Version = "1.0.0", PackageRegistration = packageRegistration5, Description = description }, result5 });

                // Same user in organization and as individual account
                var packageRegistration6 = CreatePackageRegistration(6);
                var user61 = new User() { Username = "user6.1", Key = 61 };
                packageRegistration6.Owners.Add(new Organization()
                {
                    Username = "userOrg6",
                    Key = 60,
                    Members = new List<Membership>{new Membership(){Member = user61, MemberKey = user61.Key, OrganizationKey = 60}}
                });
                packageRegistration6.Owners.Add(user61);
                var result6 = true;
                result.Add(new object[] { new Package() { Key = 6, Version = "1.0.0", PackageRegistration = packageRegistration6, Description = description }, result6 });

                // One organization with two members
                var packageRegistration7 = CreatePackageRegistration(7);
                var user71 = new User() { Username = "user7.1", Key = 71 };
                var user72 = new User() { Username = "user7.2", Key = 72 };
                packageRegistration7.Owners.Add(new Organization()
                {
                    Username = "userOrg7",
                    Key = 70,
                    Members = new List<Membership>{new Membership(){Member = user71, MemberKey = user71.Key, OrganizationKey = 70},
                                                   new Membership(){Member = user72, MemberKey = user72.Key, OrganizationKey = 70}}
                });
                var result7 = false;
                result.Add(new object[] { new Package() { Key = 7, Version = "1.0.0", PackageRegistration = packageRegistration7, Description = description }, result7 });

                // Two organizations with same member
                var packageRegistration8 = CreatePackageRegistration(9);
                var user81 = new User() { Username = "user8.1", Key = 81 };
                packageRegistration8.Owners.Add(new Organization()
                {
                    Username = "userOrg81",
                    Key = 801,
                    Members = new List<Membership>{new Membership(){Member = user81, MemberKey = user81.Key, OrganizationKey = 801}}
                });
                packageRegistration8.Owners.Add(new Organization()
                {
                    Username = "userOrg82",
                    Key = 802,
                    Members = new List<Membership>{new Membership(){Member = user81, MemberKey = user81.Key, OrganizationKey = 802}}
                });
                var result8 = true;
                result.Add(new object[] { new Package() { Key = 8, Version = "1.0.0", PackageRegistration = packageRegistration8, Description = description }, result8 });

                // Organization with suborganization with one member 
                var packageRegistration9 = CreatePackageRegistration(9);
                var user91 = new User() { Username = "user9.1", Key = 91 };
                var org91 = new Organization()
                {
                    Username = "org9Child",
                    Key = 902,
                    Members = new List<Membership>{new Membership(){Member = user91, MemberKey = user91.Key, OrganizationKey = 902}}
                };
                packageRegistration9.Owners.Add(new Organization()
                {
                    Username = "userOrgParent",
                    Key = 901,
                    Members = new List<Membership>{new Membership(){Member = org91, MemberKey = org91.Key, OrganizationKey = 901}}
                });
                var result9 = true;
                result.Add(new object[] { new Package() { Key = 9, Version = "1.0.0", PackageRegistration = packageRegistration9, Description = description }, result9 });

                // Organization with suborganization with one member and one individual user account
                var packageRegistration10 = CreatePackageRegistration(10);
                var user101 = new User() { Username = "user10.1", Key = 101 };
                var org101 = new Organization()
                {
                    Username = "org101Child",
                    Key = 1002,
                    Members = new List<Membership>{new Membership(){Member = user101, MemberKey = user101.Key, OrganizationKey = 1002}}
                };
                packageRegistration10.Owners.Add(new Organization()
                {
                    Username = "userOrgParent",
                    Key = 1001,
                    Members = new List<Membership>{new Membership(){Member = org101, MemberKey = org101.Key, OrganizationKey = 1001}}
                });
                packageRegistration10.Owners.Add(user101);
                var result10 = true;
                result.Add(new object[] { new Package() { Key = 10, Version = "1.0.0", PackageRegistration = packageRegistration10, Description = description }, result10 });

                // Organization with suborganization with one member and one individual different user account
                var packageRegistration11 = CreatePackageRegistration(11);
                var user111 = new User() { Username = "user11.1", Key = 111 };
                var user112 = new User() { Username = "user11.2", Key = 112 };
                var org111 = new Organization()
                {
                    Username = "org111Child",
                    Key = 1102,
                    Members = new List<Membership>{new Membership(){Member = user111, MemberKey = user111.Key, OrganizationKey = 1102}}
                };
                packageRegistration11.Owners.Add(new Organization()
                {
                    Username = "userOrgParent",
                    Key = 1101,
                    Members = new List<Membership>{new Membership(){Member = org111, MemberKey = org111.Key, OrganizationKey = 1101}}
                });
                packageRegistration11.Owners.Add(user112);
                var result11 = false;
                result.Add(new object[] { new Package() { Key = 11, Version = "1.0.0", PackageRegistration = packageRegistration11, Description = description }, result11 });

                return result;
            }
        }

        static PackageRegistration CreatePackageRegistration(int key)
        {
            return new PackageRegistration() { Key = 1, Id = $"regKey{key}" };
        }
    }
}
