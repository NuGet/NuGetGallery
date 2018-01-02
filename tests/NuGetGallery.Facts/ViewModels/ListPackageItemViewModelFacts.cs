﻿using System;
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
            var packageViewModel = new ListPackageItemViewModel(package, null);
            Assert.Equal("1.3.0", packageViewModel.Version);
        }

        [Fact]
        public void UsesNormalizedPackageVersionIfNormalizedVersionMissing()
        {
            var package = new Package()
            {
                Version = "01.02.00.00"
            };
            var packageViewModel = new ListPackageItemViewModel(package, null);
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
            var packageViewModel = new ListPackageItemViewModel(package, null);
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
            var packageViewModel = new ListPackageItemViewModel(package, null);
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
            var packageViewModel = new ListPackageItemViewModel(package, null);
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
            var packageViewModel = new ListPackageItemViewModel(package, null);
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
            var packageViewModel = new ListPackageItemViewModel(package, null);
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

            var listPackageItemViewModel = new ListPackageItemViewModel(package, null);

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

            var listPackageItemViewModel = new ListPackageItemViewModel(package, null);

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

            var listPackageItemViewModel = new ListPackageItemViewModel(package, null);

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

            var listPackageItemViewModel = new ListPackageItemViewModel(package, null);

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

            var listPackageItemViewModel = new ListPackageItemViewModel(package, null);

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
                Authors = authors,
                FlattenedAuthors = flattenedAuthors
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package, null);

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

            var listPackageItemViewModel = new ListPackageItemViewModel(package, null);
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

            var listPackageItemViewModel = new ListPackageItemViewModel(package, null);
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
    }
}
