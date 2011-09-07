using Xunit;

namespace NuGetGallery.ViewModels {
    public class SubmitPackageViewModelFacts {
        [Fact]
        public void SubmitPackageViewModelDescriptionHasCorrectFormatting() {
            // Arrange
            const string description = "A really cool package description.\r\nAnd more information about this package.\nEven more information.\r";
            const string formattedDescription = "A really cool package description.<br />And more information about this package.<br />Even more information.<br />";

            // Act
            var package = new SubmitPackageViewModel { Description = description };
            
            // Assert
            Assert.Equal(formattedDescription, package.Description);
        }
    }
}
