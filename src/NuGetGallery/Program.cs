// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

			// Configure Authentication
			var requireSsl = builder.Configuration.GetValue<bool>("Gallery:RequireSSL");
			var cookieSecurity = requireSsl ? CookieSecurePolicy.Always : CookieSecurePolicy.None;

			builder.Services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = "LocalUser";
				options.DefaultChallengeScheme = "LocalUser";
				options.DefaultSignInScheme = "External";
			})
			.AddCookie("LocalUser", options =>
			{
				options.LoginPath = "/users/account/LogOn";
				options.ExpireTimeSpan = TimeSpan.FromHours(6);
				options.SlidingExpiration = true;
				options.Cookie.HttpOnly = true;
				options.Cookie.SecurePolicy = cookieSecurity;
				options.Cookie.Name = ".AspNet.LocalUser";
			})
			.AddCookie("External", options =>
			{
				options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
				options.Cookie.HttpOnly = true;
				options.Cookie.SecurePolicy = cookieSecurity;
				options.Cookie.Name = ".AspNet.External";
			});

			// Configure external authentication providers
			// Microsoft Account
			var msaClientId = builder.Configuration["Auth:MicrosoftAccount:ClientId"];
			var msaClientSecret = builder.Configuration["Auth:MicrosoftAccount:ClientSecret"];
			if (!string.IsNullOrEmpty(msaClientId) && !string.IsNullOrEmpty(msaClientSecret))
			{
				builder.Services.AddAuthentication()
					.AddMicrosoftAccount("MicrosoftAccount", options =>
					{
						options.ClientId = msaClientId;
						options.ClientSecret = msaClientSecret;
						options.SignInScheme = "External";
						options.SaveTokens = true;
						options.Scope.Add("wl.emails");
						options.Scope.Add("wl.signin");
					});
			}

			// Azure Active Directory v2 (OpenID Connect)
			var aadClientId = builder.Configuration["Auth:AzureActiveDirectoryV2:ClientId"];
			var aadClientSecret = builder.Configuration["Auth:AzureActiveDirectoryV2:ClientSecret"];
			var aadTenantId = builder.Configuration["Auth:AzureActiveDirectoryV2:TenantId"] ?? "common";
			if (!string.IsNullOrEmpty(aadClientId) && !string.IsNullOrEmpty(aadClientSecret))
			{
				builder.Services.AddAuthentication()
					.AddOpenIdConnect("AzureActiveDirectoryV2", options =>
					{
						options.ClientId = aadClientId;
						options.ClientSecret = aadClientSecret;
						options.Authority = $"https://login.microsoftonline.com/{aadTenantId}/v2.0";
						options.CallbackPath = "/users/account/authenticate/return";
						options.SignInScheme = "External";
						options.SaveTokens = true;
						options.ResponseType = "code";
						options.Scope.Add("openid");
						options.Scope.Add("profile");
						options.Scope.Add("email");
						options.GetClaimsFromUserInfoEndpoint = true;
					});
			}

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

			// Configure HTTPS redirection (uses ASP.NET Core built-in)
			var requireSsl = builder.Configuration.GetValue<bool>("Gallery:RequireSSL");
			if (requireSsl)
			{
				// TODO: Implement exclusion for health probe endpoints (/api/health-probe, /api/status)
				// These endpoints need to remain accessible over HTTP for load balancers and monitoring.
				// Options: 1) Custom middleware before UseHttpsRedirection, 2) Endpoint-level configuration
				// See task 07.13 for implementation
				app.UseHttpsRedirection();
			}

			// Content Security Policy middleware (migrated from OwinStartup)
			app.UseMiddleware<NuGetGallery.Middleware.ContentSecurityPolicyMiddleware>();

			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
				app.UseHsts();
			}
			else
			{
				app.UseDeveloperExceptionPage();
			}

			// Static files from wwwroot
			app.UseStaticFiles();

			// Routing
			app.UseRouting();

			// Authentication & Authorization
			app.UseAuthentication();
			app.UseAuthorization();

			// Session
			app.UseSession();

			// Handle unobserved task exceptions (migrated from OwinStartup)
			TaskScheduler.UnobservedTaskException += (sender, exArgs) =>
			{
				// Send to AppInsights
				try
				{
					var telemetryService = app.Services.GetService<ITelemetryService>();
					if (telemetryService != null)
					{
						telemetryService.TrackException(exArgs.Exception, new Dictionary<string, string>()
						{
							{"ExceptionOrigin", "UnobservedTaskException"}
						});
					}
				}
				catch (Exception)
				{
					// Swallow exception to prevent crashing the process
				}

				exArgs.SetObserved();
			};

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
