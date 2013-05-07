using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Moq.Language.Flow;
using NuGetGallery.Commands;

namespace NuGetGallery
{
    public static class Testable
    {
        public static TController Get<TController>() where TController : NuGetControllerBase
        {
            var ctor = typeof(TController).GetConstructor(new [] { typeof(CommandExecutor) });
            Debug.Assert(ctor != null, "Testable.Get can only be used for controllers which have a constructor that only accepts a CommandExecutor");

            return (TController)ctor.Invoke(new object[] { new Mock<CommandExecutor>() { CallBase = true }.Object });
        }
    }

    public static class TestableNuGetControllerExtensions
    {
        public static ISetup<CommandExecutor, TResult> OnExecute<TResult>(this NuGetControllerBase self, Query<TResult> match)
        {
            var mockExecutor = Mock.Get(self.Executor);
            Debug.Assert(mockExecutor != null, "OnExecute can only be called on AppControllers returned by Testable.Get");

            return mockExecutor.Setup(e => e.Execute(match));
        }

        public static void AssertExecuted<TCommand>(this NuGetControllerBase self) where TCommand : ICommand
        {
            var mockExecutor = Mock.Get(self.Executor);
            Debug.Assert(mockExecutor != null, "OnExecute can only be called on AppControllers returned by Testable.Get");
            mockExecutor.Verify(e => e.Execute(It.IsAny<TCommand>()));
        }

        public static void AssertExecuted<TCommand>(this NuGetControllerBase self, TCommand expected) where TCommand : ICommand
        {
            var mockExecutor = Mock.Get(self.Executor);
            Debug.Assert(mockExecutor != null, "OnExecute can only be called on AppControllers returned by Testable.Get");
            mockExecutor.Verify(e => e.Execute(expected));
        }

        public static IReturnsResult<TTarget> Returns<TTarget, TInner>(this ISetup<TTarget, Task<TInner>> self, TInner inner)
        {
            return self.Returns(Task.FromResult(inner));
        }
    }
}
