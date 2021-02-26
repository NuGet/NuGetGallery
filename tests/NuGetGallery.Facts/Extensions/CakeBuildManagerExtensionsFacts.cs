using Xunit;

namespace NuGetGallery.Extensions
{
    public class CakeBuildManagerExtensionsFacts
    {
        public class GivenACakeAddin
        {
            [Fact]
            public void ReturnsAnAddinDirective()
            {
                // Arrange
                var model = new DisplayPackageViewModel
                {
                    Id = "Cake.7zip", 
                    Version = "1.0.0",
                    Tags = new[] { "cake-addin" }
                };

                // act
                var actual = model.GetCakeInstallPackageCommand();

                // assert
                Assert.Equal("#addin nuget:?package=Cake.7zip&version=1.0.0", actual);
            }

            [Fact]
            public void ReturnsAnAddinDirectiveWithPrerelease()
            {
                // Arrange
                var model = new DisplayPackageViewModel
                {
                    Id = "Cake.7zip",
                    Version = "1.0.0",
                    Tags = new[] { "cake-addin" },
                    Prerelease = true
                };

                // act
                var actual = model.GetCakeInstallPackageCommand();

                // assert
                Assert.Equal("#addin nuget:?package=Cake.7zip&version=1.0.0&prerelease", actual);
            }
        }

        public class GivenACakeModule
        {
            [Fact]
            public void ReturnsAModuleDirective()
            {
                // Arrange
                var model = new DisplayPackageViewModel
                {
                    Id = "Cake.BuildSystems.Module",
                    Version = "1.0.0",
                    Tags = new[] { "cake-module" }
                };

                // act
                var actual = model.GetCakeInstallPackageCommand();

                // assert
                Assert.Equal("#module nuget:?package=Cake.BuildSystems.Module&version=1.0.0", actual);
            }

            [Fact]
            public void ReturnsAModuleDirectiveWithPrerelease()
            {
                // Arrange
                var model = new DisplayPackageViewModel
                {
                    Id = "Cake.BuildSystems.Module",
                    Version = "1.0.0",
                    Tags = new[] { "cake-module" },
                    Prerelease = true
                };

                // act
                var actual = model.GetCakeInstallPackageCommand();

                // assert
                Assert.Equal("#module nuget:?package=Cake.BuildSystems.Module&version=1.0.0&prerelease", actual);
            }
        }

        public class GivenACakeRecipe
        {
            [Fact]
            public void ReturnsALoadDirective()
            {
                // Arrange
                var model = new DisplayPackageViewModel
                {
                    Id = "Cake.Recipe",
                    Version = "1.0.0",
                    Tags = new[] { "cake-recipe" }
                };

                // act
                var actual = model.GetCakeInstallPackageCommand();

                // assert
                Assert.Equal("#load nuget:?package=Cake.Recipe&version=1.0.0", actual);
            }

            [Fact]
            public void ReturnsALoadDirectiveWithPrerelease()
            {
                // Arrange
                var model = new DisplayPackageViewModel
                {
                    Id = "Cake.Recipe",
                    Version = "1.0.0",
                    Tags = new[] { "cake-recipe" },
                    Prerelease = true
                };

                // act
                var actual = model.GetCakeInstallPackageCommand();

                // assert
                Assert.Equal("#load nuget:?package=Cake.Recipe&version=1.0.0&prerelease", actual);
            }
        }

        public class GivenANonCakePackage
        {
            [Fact]
            public void ReturnsMultipleDirectives()
            {
                // Arrange
                var model = new DisplayPackageViewModel
                {
                    Id = "Polly",
                    Version = "1.0.0",
                    Tags = new[] { "" }
                };

                // act
                var actual = model.GetCakeInstallPackageCommand();

                // assert
                Assert.Contains("#addin nuget:?package=Polly&version=1.0.0", actual);
                Assert.Contains("#tool nuget:?package=Polly&version=1.0.0", actual);
            }

            [Fact]
            public void ReturnsALoadDirectiveWithPrerelease()
            {
                // Arrange
                var model = new DisplayPackageViewModel
                {
                    Id = "Polly",
                    Version = "1.0.0",
                    Tags = new[] { "" },
                    Prerelease = true
                };

                // act
                var actual = model.GetCakeInstallPackageCommand();

                // assert
                Assert.Contains("#addin nuget:?package=Polly&version=1.0.0&prerelease", actual);
                Assert.Contains("#tool nuget:?package=Polly&version=1.0.0&prerelease", actual);
            }
        }
    }
}
