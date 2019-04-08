// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using System.Web;
using Autofac;
using Microsoft.Owin;
using Moq;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Authentication;

namespace NuGetGallery.Framework
{
    internal class UnitTestBindings : Module
    {
        internal static IContainer CreateContainer(bool autoMock)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<UnitTestBindings>();

            if (autoMock)
            {
                builder.RegisterModule<AuthDependenciesModule>();
                builder.RegisterSource(new LooseMocksRegistrationSource(
                    new MockRepository(MockBehavior.Loose)
                    {
                        CallBase = true
                    }, false));
            }

            var container = builder.Build();
            return container;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var fakes = new Fakes();

            builder.RegisterInstance(fakes)
                .As<Fakes>()
                .SingleInstance();

            builder.RegisterType<TestAuditingService>()
                .As<IAuditingService>();

            builder.Register(_ =>
            {
                var mockContext = new Mock<HttpContextBase>();
                mockContext.Setup(c => c.Request.Url).Returns(new Uri(TestUtility.GallerySiteRootHttps));
                mockContext.Setup(c => c.Request.IsSecureConnection).Returns(true);
                mockContext.Setup(c => c.Request.ApplicationPath).Returns("/");
                mockContext.Setup(c => c.Response.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);
                return mockContext.Object;
            })
                .As<HttpContextBase>()
                .SingleInstance();

            builder.Register(_ =>
                    {
                        var mockService = new Mock<IPackageService>();
                        
                        foreach (var packageRegistration in fakes.PackageRegistrations)
                        {
                            mockService
                                .Setup(p => p.FindPackageRegistrationById(packageRegistration.Id))
                                .Returns(packageRegistration);

                            foreach (var package in packageRegistration.Packages)
                            {
                                mockService
                                    .Setup(p => p.FindPackageByIdAndVersion(
                                        packageRegistration.Id,
                                        package.Version,
                                        It.Is<int?>(x => package.SemVerLevelKey == null || package.SemVerLevelKey == SemVerLevelKey.Unknown || (x != null && package.SemVerLevelKey <= x)),
                                        It.Is<bool>(x => x || !package.IsPrerelease)))
                                    .Returns(package);
                            }
                        }
                        return mockService.Object;
                    })
                .As<IPackageService>()
                .SingleInstance();

            builder.Register(_ =>
            {
                var mockService = new Mock<IUserService>();

                foreach (var user in fakes.Users)
                {
                    mockService
                    .Setup(u => u.FindByUsername(user.Username))
                    .Returns(user);
                }

                return mockService.Object;
            }).As<IUserService>()
              .SingleInstance();

            builder.Register(_ =>
                    {
                        var ctxt = new FakeEntitiesContext();
                        fakes.ConfigureEntitiesContext(ctxt);
                        return ctxt;
                    })
                .As<IEntitiesContext>()
                .SingleInstance();

            builder.Register(_ => Fakes.CreateOwinContext())
                .As<IOwinContext>()
                .SingleInstance();

            var configurationService = CreateTestConfigurationService();
            UrlHelperExtensions.SetConfigurationService(configurationService);

            builder.Register(_ => configurationService)
                .As<IGalleryConfigurationService>()
                .SingleInstance();

            builder.Register(_ => configurationService.Current)
                .As<IAppConfiguration>()
                .SingleInstance();

            builder.RegisterType<CredentialBuilder>().As<ICredentialBuilder>().SingleInstance();
            builder.RegisterType<CredentialValidator>().As<ICredentialValidator>().SingleInstance();
            builder.RegisterType<DateTimeProvider>().As<IDateTimeProvider>().SingleInstance();

            builder.RegisterType<AutocompleteCveIdsQuery>()
                .As<IAutocompleteCveIdsQuery>()
                .InstancePerLifetimeScope();

            builder.RegisterType<AutocompleteCweIdsQuery>()
                .As<IAutocompleteCweIdsQuery>()
                .InstancePerLifetimeScope();

            builder.RegisterType<VulnerabilityAutocompleteService>()
                .As<IVulnerabilityAutocompleteService>()
                .InstancePerLifetimeScope();
        }

        private static IGalleryConfigurationService CreateTestConfigurationService()
        {
            // We configure HTTP site root, but require SSL.
            var configurationService = new TestGalleryConfigurationService();
            configurationService.Current.SiteRoot = TestUtility.GallerySiteRootHttp;
            configurationService.Current.RequireSSL = true;
            configurationService.Current.GalleryOwner = new MailAddress("support@example.com");

            return configurationService;
        }
    }
}
