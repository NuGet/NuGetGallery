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
            public async Task ShouldExecuteTheProvidedCommand()
            {
                // Arrange
                var executor = new TestableCommandExecutor();
                var command = new Mock<ICommand>();
                command.Setup(c => c.Execute()).Returns(Task.FromResult(0));

                // Act
                await executor.Execute(command.Object);

                // Assert
                command.Verify(c => c.Execute());
            }

            [Fact]
            public async Task ShouldExecuteTheProvidedQuery()
            {
                // Arrange
                var executor = new TestableCommandExecutor();
                var query = new Mock<IQuery>();
                query.Setup(q => q.Execute()).Returns(Task.FromResult((object)0));

                // Act
                await executor.Execute(query.Object);

                // Assert
                query.Verify(q => q.Execute());
            }

            [Fact]
            public async Task ShouldReturnTheResultOfExecutingTheProvidedQuery()
            {
                // Arrange
                var executor = new TestableCommandExecutor();
                var query = new Mock<IQuery>();
                var expected = new object();
                query.Setup(q => q.Execute()).Returns(Task.FromResult(expected));

                // Act
                var actual = await executor.Execute(query.Object);

                // Assert
                Assert.Same(expected, actual);
            }

            [Fact]
            public async Task ShouldTraceStartAndEndOfCommandExecution()
            {
                // Arrange
                var executor = new TestableCommandExecutor();
                var command = new Mock<ICommand>();
                command.Setup(c => c.Execute()).Returns(Task.FromResult(0));
                
                // Act
                await executor.Execute(command.Object);

                // Assert
                executor.MockTrace
                        .Verify(d =>
                            d.TraceEvent(
                                TraceEventType.Start,
                                It.IsAny<int>(),
                                String.Format("Starting Execution of {0}", command.Object.GetType().Name),
                                It.IsAny<string>(),
                                It.IsAny<string>(),
                                It.IsAny<int>()));
                executor.MockTrace
                        .Verify(d =>
                            d.TraceEvent(
                                TraceEventType.Stop,
                                It.IsAny<int>(),
                                String.Format("Finished Execution of {0}", command.Object.GetType().Name),
                                It.IsAny<string>(),
                                It.IsAny<string>(),
                                It.IsAny<int>()));
            }

            [Fact]
            public async Task ShouldTraceStartAndEndOfQueryExecution()
            {
                // Arrange
                var executor = new TestableCommandExecutor();
                var query = new Mock<IQuery>();
                query.Setup(q => q.Execute()).Returns(Task.FromResult((object)0));

                // Act
                await executor.Execute(query.Object);

                // Assert
                executor.MockTrace
                        .Verify(d =>
                            d.TraceEvent(
                                TraceEventType.Start,
                                It.IsAny<int>(),
                                String.Format("Starting Execution of {0}", query.Object.GetType().Name),
                                It.IsAny<string>(),
                                It.IsAny<string>(),
                                It.IsAny<int>()));
                executor.MockTrace
                        .Verify(d =>
                            d.TraceEvent(
                                TraceEventType.Stop,
                                It.IsAny<int>(),
                                String.Format("Finished Execution of {0}", query.Object.GetType().Name),
                                It.IsAny<string>(),
                                It.IsAny<string>(),
                                It.IsAny<int>()));
            }
        }

        private class TestableCommandExecutor : CommandExecutor
        {
            public Mock<IDiagnosticsSource> MockTrace { get; private set; }

            public TestableCommandExecutor()
            {
                Trace = (MockTrace = new Mock<IDiagnosticsSource>()).Object;
            }
        }
    }
}
