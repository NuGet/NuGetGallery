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
            var packageViewModel = new ListPackageItemViewModel(package);
            Assert.Equal("1.3.0", packageViewModel.Version);
        }

        [Fact]
        public void UsesNormalizedPackageVersionIfNormalizedVersionMissing()
        {
            var package = new Package()
            {
                Version = "01.02.00.00"
            };
            var packageViewModel = new ListPackageItemViewModel(package);
            Assert.Equal("1.2.0", packageViewModel.Version);
        }

        [Fact]
        public void LicenseNamesAreParsedByCommas()
        {
            var package = new Package
            {
                LicenseNames = "l1,l2, l3 ,l4  ,  l5 ",
            };
            var packageViewModel = new ListPackageItemViewModel(package);
            Assert.Equal(new string[] { "l1", "l2", "l3", "l4", "l5" }, packageViewModel.LicenseNames);
        }

        [Fact]
        public void LicenseReportFieldsKeptWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                HideLicenseReport = true,
                LicenseNames = "l1",
                LicenseReportUrl = "url"
            };
            var packageViewModel = new ListPackageItemViewModel(package);
            Assert.NotNull(packageViewModel.LicenseNames);
            Assert.NotNull(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseReportUrlKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
                HideLicenseReport = false,
                LicenseReportUrl = "url"
            };
            var packageViewModel = new ListPackageItemViewModel(package);
            Assert.NotNull(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseNamesKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
                HideLicenseReport = false,
                LicenseNames = "l1"
            };
            var packageViewModel = new ListPackageItemViewModel(package);
            Assert.NotNull(packageViewModel.LicenseNames);
        }

        [Fact]
        public void LicenseUrlKeptWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                HideLicenseReport = true,
                LicenseUrl = "url"
            };
            var packageViewModel = new ListPackageItemViewModel(package);
            Assert.NotNull(packageViewModel.LicenseUrl);
        }
        #endregion

        [Fact]
        public void ShortDescriptionsNotTruncated()
        {
            var description = "A Short Description";
            var package = new Package()
            {
                Description = description
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package);

            Assert.Equal(description, listPackageItemViewModel.ShortDescription);
            Assert.Equal(false, listPackageItemViewModel.IsDescriptionTruncated);
        }

        [Fact]
        public void LongDescriptionsTruncated()
        {
            var description = @"A Longer description full of nonsense that will get truncated. Lorem ipsum dolor sit amet, ad nemore gubergren eam. Ea quaeque labores deseruisse his, eos munere convenire at, in eos audire persius corpora. Te his volumus detracto offendit, has ne illud choro. No illum quaestio mel, novum democritum te sea, et nam nisl officiis salutandi. Vis ut harum docendi incorrupte, nam affert putent sententiae id, mei cibo omnium id. Ea est falli graeci voluptatibus, est mollis denique ne.
An nec tempor cetero vituperata.Ius cu dicunt regione interpretaris, posse veniam facilisis ad vim, sit ei sale integre. Mel cu aliquid impedit scribentur.Nostro recusabo sea ei, nec habeo instructior no, saepe altera adversarium vel cu.Nonumes molestiae sit at, per enim necessitatibus cu.
At mei iriure dignissim theophrastus.Meis nostrud te sit, equidem maiorum pri ex.Vim dolorem fuisset an. At sit veri illum oratio, et per dicat contentiones. In eam tale tation, mei dicta labitur corpora ei, homero equidem suscipit ut eam.";

            var package = new Package()
            {
                Description = description
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package);

            Assert.NotEqual(description, listPackageItemViewModel.ShortDescription);
            Assert.Equal(true, listPackageItemViewModel.IsDescriptionTruncated);
            Assert.True(listPackageItemViewModel.ShortDescription.EndsWith("..."));
        }

        [Fact]
        public void EmptyTagsAreParsedEmpty()
        {
            var package = new Package() { };

            var listPackageItemViewModel = new ListPackageItemViewModel(package);

            Assert.Equal(null, listPackageItemViewModel.Tags);
        }

        [Fact]
        public void TagsAreParsed()
        {
            var package = new Package()
            {
                Tags = "tag1 tag2 tag3"
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package);

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
                Authors = authors,
                FlattenedAuthors = flattenedAuthors
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package);

            Assert.Equal(flattenedAuthors, listPackageItemViewModel.Authors);
        }

        [Fact]
        public void UseVersionIfLatestAndStableNotSame()
        {
            var package = new Package()
            {
                IsLatest = true,
                IsLatestStable = false
            };

            var listPackageItemViewModel = new ListPackageItemViewModel(package);
            Assert.True(listPackageItemViewModel.UseVersion);

            package.IsLatest = false;
            package.IsLatestStable = true;
            Assert.True(listPackageItemViewModel.UseVersion);

            package.IsLatest = false;
            package.IsLatestStable = false;
            Assert.True(listPackageItemViewModel.UseVersion);

            package.IsLatest = true;
            package.IsLatestStable = true;
            Assert.False(listPackageItemViewModel.UseVersion);
        }
    }
}
