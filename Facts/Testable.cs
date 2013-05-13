using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
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
            
            var controller = (TController)ctor.Invoke(new object[] { new Mock<CommandExecutor>() { CallBase = true }.Object });
            
            var httpContext = new Mock<HttpContextBase>();
            httpContext.Setup(c => c.Request).Returns(new Mock<HttpRequestBase>().Object);
            httpContext.Setup(c => c.Response).Returns(new Mock<HttpResponseBase>().Object);
            var requestContext = new RequestContext(httpContext.Object, new RouteData());
            var controllerContext = new ControllerContext(requestContext, controller);
            controller.ControllerContext = controllerContext;

            return controller;
        }
    }

    public static class TestableNuGetControllerExtensions
    {
        public static Mock<HttpContextBase> MockHttpContext(this Controller self)
        {
            var mock = Mock.Get(self.HttpContext);
            Debug.Assert(mock != null, "MockHttpContext can only be called on Controllers with a Mock HttpContextBase");
            return mock;
        }

        public static ISetup<CommandExecutor, TResult> OnExecute<TResult>(this NuGetControllerBase self, Command<TResult> match)
        {
            var mockExecutor = Mock.Get(self.Executor);
            Debug.Assert(mockExecutor != null, "OnExecute can only be called on Controllers with a mock command executor");

            return mockExecutor.Setup(e => e.Execute(match));
        }

        public static void AssertExecuted<TCommand>(this NuGetControllerBase self) where TCommand : Command
        {
            var mockExecutor = Mock.Get(self.Executor);
            Debug.Assert(mockExecutor != null, "OnExecute can only be called on Controllers with a mock command executor");
            mockExecutor.Verify(e => e.Execute(It.IsAny<TCommand>()));
        }

        public static void AssertExecuted<TCommand>(this NuGetControllerBase self, TCommand expected) where TCommand : Command
        {
            var mockExecutor = Mock.Get(self.Executor);
            Debug.Assert(mockExecutor != null, "OnExecute can only be called on Controllers with a mock command executor");
            mockExecutor.Verify(e => e.Execute(expected));
        }
    }
}
