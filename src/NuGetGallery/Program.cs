// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using Autofac;
using Autofac.Extensions.DependencyInjection;

namespace NuGetGallery
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Configure configuration sources
			builder.Configuration
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
				.AddEnvironmentVariables();

			// Tune ServicePointManager (migrated from OwinStartup)
			// Note: These settings are obsolete in .NET Core but kept for compatibility if any legacy code still uses them
#pragma warning disable SYSLIB0014, CS0618
			ServicePointManager.DefaultConnectionLimit = 500;
			ServicePointManager.UseNagleAlgorithm = false;
			ServicePointManager.Expect100Continue = false;
			ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
#pragma warning restore SYSLIB0014, CS0618

			// Setting time out for all RegEx objects
			AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(10));

			// Configure Autofac as the DI container
			builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

			// Configure Autofac container
			builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
			{
				// Register all modules from the assembly
				containerBuilder.RegisterAssemblyModules(typeof(Program).Assembly);

				// TODO: Additional Autofac registrations will be added here
				// (migrated from AutofacConfig.cs in subsequent tasks)
			});

			// Add services to the container
			builder.Services.AddControllersWithViews()
				.AddRazorRuntimeCompilation(); // Enable runtime compilation for development

			// Add API controllers
			builder.Services.AddControllers();

			// Configure Entity Framework 6 (kept per user preference)
			// EF6 doesn't need special configuration for .NET Core, it works out of the box

			// Configure session
			builder.Services.AddDistributedMemoryCache();
			builder.Services.AddSession(options =>
			{
				options.IdleTimeout = TimeSpan.FromMinutes(30);
				options.Cookie.HttpOnly = true;
				options.Cookie.IsEssential = true;
			});

			// Configure Application Insights
			var appInsightsKey = builder.Configuration["Gallery:AppInsightsInstrumentationKey"];
			if (!string.IsNullOrEmpty(appInsightsKey))
			{
				builder.Services.AddApplicationInsightsTelemetry(options =>
				{
					options.ConnectionString = $"InstrumentationKey={appInsightsKey}";
				});
			}

			// Configure Kestrel
			builder.WebHost.ConfigureKestrel(serverOptions =>
			{
				serverOptions.AddServerHeader = false;
				serverOptions.Limits.MaxRequestBodySize = 250 * 1024 * 1024; // 250MB for package uploads
			});

			var app = builder.Build();

			// Configure the HTTP request pipeline

			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
				// TODO: HSTS will be configured in middleware migration task
			}

			// Static files from wwwroot
			app.UseStaticFiles();

			// Routing
			app.UseRouting();

			// Authentication & Authorization (will be configured in subsequent tasks)
			// app.UseAuthentication();
			// app.UseAuthorization();

			// Session
			app.UseSession();

			// TODO: Middleware pipeline will be fully configured in task 07.06

			// Map controllers
			app.MapControllers();
			app.MapControllerRoute(
				name: "default",
				pattern: "{controller=Home}/{action=Index}/{id?}");

			// Map Razor Pages (if any)
			app.MapRazorPages();

			app.Run();
		}
	}
}
