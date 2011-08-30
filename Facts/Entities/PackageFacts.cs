using Xunit;

namespace NuGetGallery.Entities
{
    public class PackageFacts {
        public class ThePackageClass {
            [Fact]
            public void WillContainCorrectFormatting() {
                const string description = "A really cool package description.\r\nAnd more information about this package.\nEven more informatoin.\r";
                const string formattedDescription = "A really cool package description.<br />And more information about this package.<br />Even more informatoin.<br />";

                var package = new Package { Description = description };
                Assert.Equal(formattedDescription, package.Description);
            }
        }
    }
}