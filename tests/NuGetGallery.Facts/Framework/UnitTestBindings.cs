using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Ninject;
using Ninject.Activation;
using Ninject.MockingKernel;
using Ninject.MockingKernel.Moq;
using Ninject.Modules;
using Ninject.Planning.Bindings;

namespace NuGetGallery.Framework
{
    internal class UnitTestBindings : NinjectModule
    {
        internal static IKernel CreateContainer()
        {
            return new TestKernel(new UnitTestBindings());
        }

        public override void Load()
        {
            Bind<HttpContextBase>()
                .ToMethod(_ =>
                {
                    var mockContext = new Mock<HttpContextBase>();
                    mockContext.Setup(c => c.User).Returns(Fakes.User.Principal);
                    mockContext.Setup(c => c.Request.Url).Returns(new Uri("https://nuget.local/"));
                    mockContext.Setup(c => c.Request.ApplicationPath).Returns("/");
                    mockContext.Setup(c => c.Response.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);
                    return mockContext.Object;
                })
                .InSingletonScope();

            Bind<IPackageService>()
                .ToMethod(_ =>
                {
                    var mockService = new Mock<IPackageService>();
                    mockService
                        .Setup(p => p.FindPackageRegistrationById(Fakes.Package.Id))
                        .Returns(Fakes.Package);
                    return mockService.Object;
                })
                .InSingletonScope();

            Bind<IUserService>()
                .ToMethod(_ =>
                {
                    var mockService = new Mock<IUserService>();
                    mockService.Setup(u => u.FindByUsername(Fakes.User.UserName)).Returns(Fakes.User.User);
                    mockService.Setup(u => u.FindByUsername(Fakes.Owner.UserName)).Returns(Fakes.Owner.User);
                    mockService.Setup(u => u.FindByUsername(Fakes.Admin.UserName)).Returns(Fakes.Admin.User);
                    return mockService.Object;
                });
        }

        private class TestKernel : MoqMockingKernel
        {
            public TestKernel(params NinjectModule[] modules) : base(new NinjectSettings(), modules) { }
        }
    }
}
