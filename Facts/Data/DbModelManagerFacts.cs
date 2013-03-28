using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Moq;
using Xunit;

namespace NuGetGallery.Data
{
    public class DbModelManagerFacts
    {
        private static readonly DbCompiledModel TestModel = new DbModelBuilder(DbModelBuilderVersion.Latest)
            .Build(new DbProviderInfo("System.Data.SqlClient", "2008"))
            .Compile();
        private static readonly DbCompiledModel RebuiltTestModel = new DbModelBuilder(DbModelBuilderVersion.Latest)
            .Build(new DbProviderInfo("System.Data.SqlClient", "2008"))
            .Compile();

        public class TheGetCurrentModelMethod
        {
            [Fact]
            public void ItCreatesTheModelIfItHasntBeenCreatedYet()
            {
                // Arrange
                var manager = new TestableDbModelManager();
                manager.MockModelFactory.Setup(f => f.CreateModel()).Returns(TestModel);

                // Act
                var model = manager.GetCurrentModel();

                // Assert
                Assert.Same(TestModel, model);
            }

            [Fact]
            public void ItReturnsTheSameModelInstanceIfCalledMultipleTimes()
            {
                // Arrange
                var manager = new TestableDbModelManager();
                manager.MockModelFactory.Setup(f => f.CreateModel()).Returns(TestModel);

                // Act
                var modelOne = manager.GetCurrentModel();
                var modelTwo = manager.GetCurrentModel();

                // Assert
                Assert.Same(TestModel, modelOne);
                Assert.Same(TestModel, modelTwo);
            }
        }

        public class TheRebuildModelMethod
        {
            [Fact]
            public void ItReturnsTheSameModelInstanceIfCalledMultipleTimes()
            {
                // Arrange
                var manager = new TestableDbModelManager();
                manager.MockModelFactory.Setup(f => f.CreateModel()).Returns(TestModel);
                var originalModel = manager.GetCurrentModel();
                manager.MockModelFactory.Setup(f => f.CreateModel()).Returns(RebuiltTestModel);

                // Assume
                Assert.Same(TestModel, originalModel);

                // Act
                manager.RebuildModel();
                var rebuiltModel = manager.GetCurrentModel();

                // Assert
                Assert.Same(RebuiltTestModel, rebuiltModel);
            }
        }

        public class TestableDbModelManager : DbModelManager
        {
            public Mock<IDbModelFactory> MockModelFactory { get; private set; }

            public TestableDbModelManager()
            {
                ModelFactory = (MockModelFactory = new Mock<IDbModelFactory>()).Object;
            }
        }
    }
}
