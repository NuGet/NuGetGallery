// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Autofac;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery.Framework
{
    public class TestContainer : IDisposable
    {
        public IContainer Container { get; private set; }

        public TestContainer() : this(UnitTestBindings.CreateContainer(autoMock: true)) { }

        protected TestContainer(IContainer container)
        {
            // Initialize the container
            Container = container;
        }

        protected TController GetController<TController>()
            where TController : Controller
        {
            if (!Container.IsRegistered(typeof(TController)))
            {
                var updater = new ContainerBuilder();
                updater.RegisterType<TController>().PropertiesAutowired().AsSelf();
            }

            var c = Container.Resolve<TController>();

            Container.InjectMockProperties(Mock.Get(c));

            c.ControllerContext = new ControllerContext(
                new RequestContext(Container.Resolve<HttpContextBase>(), new RouteData()), c);

            var routeCollection = new RouteCollection();
            Routes.RegisterRoutes(routeCollection);
            c.Url = new UrlHelper(c.ControllerContext.RequestContext, routeCollection);

            var appCtrl = c as AppController;
            if (appCtrl != null)
            {
                appCtrl.SetOwinContextOverride(Fakes.CreateOwinContext());
                appCtrl.NuGetContext.Config = GetConfigurationService();
            }

            return c;
        }

        protected TestGalleryConfigurationService GetConfigurationService()
        {
            return Container.Resolve<IGalleryConfigurationService>() as TestGalleryConfigurationService;
        }

        protected TService GetService<TService>()
        {
            var updater = new ContainerBuilder();
            updater.RegisterType<TService>().AsImplementedInterfaces().AsSelf();

            return Get<TService>();
        }

        protected FakeEntitiesContext GetFakeContext()
        {
            var fakeContext = Container.Resolve<IEntitiesContext>() as FakeEntitiesContext;

            if (fakeContext == null)
            {
                fakeContext = new FakeEntitiesContext();
            }

            var updater = new ContainerBuilder();
            updater.RegisterInstance(fakeContext).As<IEntitiesContext>();

            updater.RegisterInstance(new EntityRepository<Package>(fakeContext))
                .As<IEntityRepository<Package>>();

            updater.RegisterInstance(new EntityRepository<PackageOwnerRequest>(fakeContext))
                .As<IEntityRepository<PackageOwnerRequest>>();

            updater.RegisterInstance(new EntityRepository<PackageRegistration>(fakeContext))
                .As<IEntityRepository<PackageRegistration>>();

            updater.RegisterInstance(new EntityRepository<PackageRename>(fakeContext))
                .As<IEntityRepository<PackageRename>>();

#pragma warning disable CS0618 // Type or member is obsolete
            updater.Update(Container);
#pragma warning restore CS0618 // Type or member is obsolete

            return fakeContext;
        }

        protected T Get<T>()
        {
            if (typeof(Controller).IsAssignableFrom(typeof(T)))
            {
                throw new InvalidOperationException("Use GetController<T> to get a controller instance");
            }

            return Container.Resolve<T>();
        }

        protected Mock<T> GetMock<T>() where T : class
        {
            bool registerMock = false;
            if (Container.IsRegistered(typeof(T)))
            {
                try
                {
                    Mock.Get(Container.Resolve<T>());
                }
                catch
                {
                    registerMock = true;
                }
            }

            if (registerMock || !Container.IsRegistered(typeof(T)))
            {
                var mockInstance = (new Mock<T>() { CallBase = true }).Object;

                var updater = new ContainerBuilder();
                updater.RegisterInstance(mockInstance).As<T>();
#pragma warning disable CS0618 // Type or member is obsolete
                updater.Update(Container);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            T instance = Container.Resolve<T>();
            return Mock.Get(instance);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Container != null)
            {
                Container.Dispose();
                Container = null;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}