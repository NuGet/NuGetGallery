// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Web.Http;
using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;
using Autofac.Integration.WebApi;
using Owin;

namespace NuGetGallery
{
    public static class AutofacConfig
    {
        public static IAppBuilder UseAutofacInjection(this IAppBuilder app, HttpConfiguration httpConfiguration)
        {
            var currentAssembly = Assembly.GetExecutingAssembly();

            var builder = new ContainerBuilder();

            // Web API
            builder.RegisterApiControllers(currentAssembly);
            builder.RegisterWebApiFilterProvider(httpConfiguration);

            // MVC
            builder.RegisterControllers(currentAssembly).PropertiesAutowired();

            builder.RegisterModelBinders(currentAssembly);
            builder.RegisterModelBinderProvider();

            builder.RegisterModule<AutofacWebTypesModule>();
            //builder.RegisterSource(new ViewRegistrationSource());

            builder.RegisterFilterProvider();

            builder.RegisterAssemblyModules(currentAssembly);
            
            // Hook it up
            var container = builder.Build();
            httpConfiguration.DependencyResolver = new AutofacWebApiDependencyResolver(container);
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

            // Register the Autofac middleware FIRST, then the Autofac MVC middleware.
            app.UseAutofacMiddleware(container);
            app.UseAutofacMvc();

            return app;
        }
    }
}