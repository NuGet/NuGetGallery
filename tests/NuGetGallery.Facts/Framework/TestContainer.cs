// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Microsoft.Owin;
using Moq;
using Ninject;
using NuGetGallery.Configuration;

namespace NuGetGallery.Framework
{
    public class TestContainer : IDisposable
    {
        public IKernel Kernel { get; private set; }

        public TestContainer() : this(UnitTestBindings.CreateContainer(autoMock: true)) { }
        protected TestContainer(IKernel kernel)
        {
            // Initialize the container
            Kernel = kernel;
        }

        protected TController GetController<TController>() where TController : Controller
        {
            if (!Kernel.GetBindings(typeof(TController)).Any())
            {
                Kernel.Bind<TController>().ToSelf();
            }
            var c = Kernel.Get<TController>();
            c.ControllerContext = new ControllerContext(
                new RequestContext(Kernel.Get<HttpContextBase>(), new RouteData()), c);

            var routeCollection = new RouteCollection();
            Routes.RegisterRoutes(routeCollection);
            c.Url = new UrlHelper(c.ControllerContext.RequestContext, routeCollection);

            var appCtrl = c as AppController;
            if (appCtrl != null)
            {
                appCtrl.OwinContext = Kernel.Get<IOwinContext>();
                appCtrl.NuGetContext.Config = Kernel.Get<ConfigurationService>();
            }

            return c;
        }

        protected TService GetService<TService>()
        {
            var serviceInterfaces = typeof(TService).GetInterfaces();
            Kernel.Bind(serviceInterfaces).To(typeof(TService));
            return Get<TService>();
        }

        protected FakeEntitiesContext GetFakeContext()
        {
            var fakeContext = new FakeEntitiesContext();
            Kernel.Bind<IEntitiesContext>().ToConstant(fakeContext);
            Kernel.Bind<IEntityRepository<Package>>().ToConstant(new EntityRepository<Package>(fakeContext));
            Kernel.Bind<IEntityRepository<PackageOwnerRequest>>().ToConstant(new EntityRepository<PackageOwnerRequest>(fakeContext));
            Kernel.Bind<IEntityRepository<PackageStatistics>>().ToConstant(new EntityRepository<PackageStatistics>(fakeContext));
            Kernel.Bind<IEntityRepository<PackageRegistration>>().ToConstant(new EntityRepository<PackageRegistration>(fakeContext));
            return fakeContext;
        }

        protected T Get<T>()
        {
            if(typeof(Controller).IsAssignableFrom(typeof(T))) {
                throw new InvalidOperationException("Use GetController<T> to get a controller instance");
            }
            return Kernel.Get<T>();
        }

        protected Mock<T> GetMock<T>() where T : class
        {
            if (!Kernel.GetBindings(typeof(T)).Any())
            {
                Kernel.Bind<T>().ToConstant((new Mock<T>() { CallBase = true }).Object);
            }
            T instance = Kernel.Get<T>();
            return Mock.Get(instance);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Kernel != null)
            {
                Kernel.Dispose();
                Kernel = null;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}

