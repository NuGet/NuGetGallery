// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using Autofac;
using Microsoft.Owin;
using Moq;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;

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
            builder.RegisterType<TestAuditingService>()
                .As<AuditingService>();

            builder.Register(_ =>
            {
                var mockContext = new Mock<HttpContextBase>();
                mockContext.Setup(c => c.Request.Url).Returns(new Uri("https://nuget.local/"));
                mockContext.Setup(c => c.Request.ApplicationPath).Returns("/");
                mockContext.Setup(c => c.Response.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);
                return mockContext.Object;
            })
                .As<HttpContextBase>()
                .SingleInstance();

            builder.Register(_ =>
                    {
                        var mockService = new Mock<IPackageService>();
                        mockService
                            .Setup(p => p.FindPackageRegistrationById(Fakes.Package.Id))
                            .Returns(Fakes.Package);
                        return mockService.Object;
                    })
                .As<IPackageService>()
                .SingleInstance();

            builder.Register(_ =>
            {
                var mockService = new Mock<IUserService>();
                mockService.Setup(u => u.FindByUsername(Fakes.User.Username)).Returns(Fakes.User);
                mockService.Setup(u => u.FindByUsername(Fakes.Owner.Username)).Returns(Fakes.Owner);
                mockService.Setup(u => u.FindByUsername(Fakes.Admin.Username)).Returns(Fakes.Admin);
                return mockService.Object;
            })
                .As<IUserService>()
                .SingleInstance();

            builder.Register(_ =>
                    {
                        var ctxt = new FakeEntitiesContext();
                        Fakes.ConfigureEntitiesContext(ctxt);
                        return ctxt;
                    })
                .As<IEntitiesContext>()
                .SingleInstance();

            builder.Register(_ => Fakes.CreateOwinContext())
                .As<IOwinContext>()
                .SingleInstance();
        }
    }
}
