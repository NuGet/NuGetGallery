using System.Collections.Generic;
using Moq;
using Xunit;

namespace NuGetGallery.Data
{
    public class DbModelFactoryFacts
    {
        internal const string MigrationName = "AMigration";
        public class TheCreateModelMethod
        {
            [Fact]
            public void ItUpdatesTheDatabaseToTheMinimum()
            {
                // Arrange
                var factory = new TestableDbModelFactory();

                // Act
                factory.CreateModel();

                // Assert
                factory.MockVersioningService.Verify(v => v.UpdateToMinimum());
            }

            [Fact]
            public void ItCreatesAModelWithEverythingIfVersioningServiceIsNull()
            {
                // Arrange
                var factory = new TestableDbModelFactory(withVersioningService: false);

                // Act
                var model = factory.CreateModel();

                // Assert
                // No real way to crack the model open and verify it... We would have gotten exceptions if it was invalid though
                Assert.NotNull(model);
            }
        }

        public class TestableDbModelFactory : DbModelFactory
        {
            public Mock<IDatabaseVersioningService> MockVersioningService { get; private set; }

            public TestableDbModelFactory(bool withVersioningService = true)
            {
                ModelsAssembly = typeof(TestModels.EmptyModel).Assembly;
                ModelsNamespace = typeof(TestModels.EmptyModel).Namespace;
                if (withVersioningService)
                {
                    VersioningService = (MockVersioningService = new Mock<IDatabaseVersioningService>()).Object;

                    var emptySet = new HashSet<string>();
                    MockVersioningService.Setup(v => v.AppliedVersions).Returns(emptySet);
                    MockVersioningService.Setup(v => v.AvailableVersions).Returns(emptySet);
                    MockVersioningService.Setup(v => v.PendingVersions).Returns(emptySet);
                }
            }
        }
    }

    namespace TestModels
    {
        public class EmptyModel
        {
            public int Id { get; set; }
        }
        
        public class ModelWithProperties
        {
            public int Id { get; set; }
            public string Property { get; set; }
        }

        public class ModelWithPropertiesDependentUponMigrations
        {
            public int Id { get; set; }
            public string IndependentProperty { get; set; }

            [RequiresMigration(DbModelFactoryFacts.MigrationName)]
            public string DependentProperty { get; set; }
        }
    }
}