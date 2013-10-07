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
            var kernel = new TestKernel(new UnitTestBindings());
            return kernel;
        }

        internal static IKernel CreateContainer<TUnderTest>()
        {
            var kernel = new TestKernel(new UnitTestBindings());
            kernel.Bind<TUnderTest>().ToSelf();
            return kernel;
        }

        public override void Load()
        {
            Bind<HttpContextBase>()
                .ToMethod(_ =>
                {
                    var mockContext = new Mock<HttpContextBase>();
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
                    mockService.Setup(u => u.FindByUsername(Fakes.User.Username)).Returns(Fakes.User);
                    mockService.Setup(u => u.FindByUsername(Fakes.Owner.Username)).Returns(Fakes.Owner);
                    mockService.Setup(u => u.FindByUsername(Fakes.Admin.Username)).Returns(Fakes.Admin);
                    return mockService.Object;
                })
                .InSingletonScope();

            Bind<IEntitiesContext>()
                .ToMethod(_ =>
                {
                    var ctxt = new FakeEntitiesContext();
                    Fakes.ConfigureEntitiesContext(ctxt);
                    return ctxt;
                })
                .InSingletonScope();
        }

        private class TestKernel : MoqMockingKernel
        {
            public TestKernel(params NinjectModule[] modules) : base(new NinjectSettings(), modules) { }
        }
    }
}
