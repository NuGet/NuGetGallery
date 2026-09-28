// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NuGet.Services.CDNRedirect.Configuration;
using NuGet.Services.CDNRedirect.Middleware;
using NuGet.Services.CDNRedirect.Services;

namespace NuGet.Services.CDNRedirect
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateApplication(args).Run();
        }

        public static WebApplication CreateApplication(
            string[] args,
            Action<WebApplicationBuilder>? configureBuilder = null)
        {
            var builder = WebApplication.CreateBuilder(args);
            configureBuilder?.Invoke(builder);

            builder.Logging.AddApplicationInsights();
            builder.Services.AddApplicationInsightsTelemetry();
            builder.Services
                .AddOptions<RedirectOptions>()
                .Bind(builder.Configuration)
                .ValidateOnStart();
            builder.Services.AddSingleton<IValidateOptions<RedirectOptions>, RedirectOptionsValidator>();
            builder.Services.AddSingleton<IRedirectService, RedirectService>();
            builder.Services.AddScoped<IErrorViewRenderer, ErrorViewRenderer>();
            builder.Services
                .AddControllersWithViews()
                .AddApplicationPart(typeof(Program).Assembly);

            var app = builder.Build();

            app.UseRouting();
            app.UseMiddleware<RedirectMiddleware>();
            app.MapControllers();

            return app;
        }
    }
}
