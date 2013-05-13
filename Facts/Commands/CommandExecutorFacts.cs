using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery.Commands
{
    public class CommandExecutorFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public void ShouldExecuteTheProvidedCommand()
            {
                // Arrange
                var executor = new TestableCommandExecutor();
                var cmd = new DummyCommand();
                var expected = new object();
                var handler = new Mock<CommandHandler<DummyCommand, object>>();
                handler.Setup(h => h.Execute(cmd)).Returns(expected);
                executor.MockContainer
                        .Setup(c => c.GetService(typeof(CommandHandler<DummyCommand, object>)))
                        .Returns(handler.Object);

                // Act
                var actual = executor.Execute(cmd);

                // Assert
                Assert.Same(expected, actual);
            }

            [Fact]
            public void ShouldTraceStartAndEndOfCommandExecution()
            {
                // Arrange
                var executor = new TestableCommandExecutor();
                var cmd = new DummyCommand();
                var expected = new object();
                var handler = new Mock<CommandHandler<DummyCommand, object>>();
                handler.Setup(h => h.Execute(cmd)).Returns(expected);
                executor.MockContainer
                        .Setup(c => c.GetService(typeof(CommandHandler<DummyCommand, object>)))
                        .Returns(handler.Object);
                
                // Act
                executor.Execute(cmd);

                // Assert
                executor.MockTrace
                        .Verify(d =>
                            d.TraceEvent(
                                TraceEventType.Start,
                                It.IsAny<int>(),
                                "Starting Execution of DummyCommand",
                                It.IsAny<string>(),
                                It.IsAny<string>(),
                                It.IsAny<int>()));
                executor.MockTrace
                        .Verify(d =>
                            d.TraceEvent(
                                TraceEventType.Stop,
                                It.IsAny<int>(),
                                "Finished Execution of DummyCommand",
                                It.IsAny<string>(),
                                It.IsAny<string>(),
                                It.IsAny<int>()));
            }
        }

        public class DummyCommand : Command<object> { }

        private class TestableCommandExecutor : CommandExecutor
        {
            public Mock<IDiagnosticsSource> MockTrace { get { return Mock.Get(Trace); } }
            public Mock<IServiceProvider> MockContainer { get { return Mock.Get(Container); } }

            public TestableCommandExecutor() : base(Mock.Of<IServiceProvider>(), new MockDiagnosticsService()) {}
        }
    }
}
