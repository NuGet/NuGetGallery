// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;
using Owin;

namespace NuGetGallery
{
    public static class AutofacConfig
    {
        public static IAppBuilder UseAutofacInjection(this IAppBuilder app)
        {
            var currentAssembly = typeof(AutofacConfig).Assembly;

            var builder = new ContainerBuilder();

            builder.RegisterControllers(currentAssembly).PropertiesAutowired();

            builder.RegisterModelBinders(Assembly.GetExecutingAssembly());
            builder.RegisterModelBinderProvider();

            builder.RegisterModule<AutofacWebTypesModule>();

            //builder.RegisterSource(new ViewRegistrationSource());

            builder.RegisterFilterProvider();

            builder.RegisterAssemblyModules(currentAssembly);

            var container = builder.Build();
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

            // Register the Autofac middleware FIRST, then the Autofac MVC middleware.
            app.UseAutofacMiddleware(container);
            app.UseAutofacMvc();

            return app;
        }
    }
}