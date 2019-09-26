// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class RepoUtilsFacts
    {
        private static readonly IReadOnlyList<string> _expectedReferences = new[]
        {
            "Autofac.Extensions.DependencyInjection",
            "Markdig.Signed",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Http.Polly",
            "NuGet.Services.Licenses",
            "NuGet.StrongName.AnglicanGeek.MarkdownMailer",
            "Autofac",
            "Autofac.Mvc5",
            "Autofac.Mvc5.Owin",
            "Autofac.Owin",
            "Autofac.WebApi2",
            "CommonMark.NET",
            "d3",
            "NuGet.StrongName.DynamicData.EFCodeFirstProvider",
            "NuGet.StrongName.elmah.corelibrary",
            "NuGet.StrongName.elmah.sqlserver",
            "NuGet.StrongName.elmah",
            "EntityFramework",
            "Knockout.Mapping",
            "knockoutjs",
            "Lucene.Net",
            "Lucene.Net.Contrib",
            "MicroBuild.Core",
            "Microsoft.ApplicationInsights",
            "Microsoft.ApplicationInsights.Agent.Intercept",
            "Microsoft.ApplicationInsights.DependencyCollector",
            "Microsoft.ApplicationInsights.PerfCounterCollector",
            "Microsoft.ApplicationInsights.TraceListener",
            "Microsoft.ApplicationInsights.Web",
            "Microsoft.ApplicationInsights.WindowsServer",
            "Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel",
            "Microsoft.AspNet.DynamicData.EFProvider",
            "Microsoft.AspNet.Identity.Core",
            "Microsoft.AspNet.Mvc",
            "Microsoft.AspNet.Razor",
            "Microsoft.AspNet.Web.Optimization",
            "Microsoft.AspNet.WebApi.Client",
            "Microsoft.AspNet.WebApi.Core",
            "Microsoft.AspNet.WebApi.MessageHandlers.Compression.StrongName",
            "Microsoft.AspNet.WebApi.OData",
            "Microsoft.AspNet.WebApi.WebHost",
            "Microsoft.AspNet.WebHelpers",
            "Microsoft.AspNet.WebPages",
            "Microsoft.AspNet.WebPages.Data",
            "Microsoft.AspNet.WebPages.WebData",
            "Microsoft.AspNetCore.Cryptography.Internal",
            "Microsoft.AspNetCore.Cryptography.KeyDerivation",
            "Microsoft.Azure.KeyVault",
            "Microsoft.Azure.KeyVault.Core",
            "Microsoft.Bcl.Compression",
            "Microsoft.Data.Services",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.IdentityModel.Clients.ActiveDirectory",
            "Microsoft.Net.Http",
            "Microsoft.Owin",
            "Microsoft.Owin.Host.SystemWeb",
            "Microsoft.Owin.Security",
            "Microsoft.Owin.Security.Cookies",
            "Microsoft.Owin.Security.MicrosoftAccount",
            "Microsoft.Owin.Security.OpenIdConnect",
            "Microsoft.Web.Infrastructure",
            "Microsoft.Web.Xdt",
            "Microsoft.WindowsAzure.ConfigurationManager",
            "Modernizr",
            "Moment.js",
            "MvcTreeView",
            "NuGet.Configuration",
            "NuGet.Protocol",
            "NuGet.Services.KeyVault",
            "NuGet.Services.Owin",
            "NuGet.Services.Sql",
            "Owin",
            "NuGet.StrongName.QueryInterceptor",
            "RouteMagic",
            "SharpZipLib",
            "Strathweb.CacheOutput.WebApi2.StrongName",
            "System.Diagnostics.Debug",
            "System.Diagnostics.DiagnosticSource",
            "System.Linq.Expressions",
            "System.Net.Http",
            "NuGet.StrongName.WebActivator",
            "WebActivatorEx",
            "NuGet.StrongName.WebBackgrounder.EntityFramework",
            "NuGet.StrongName.WebBackgrounder",
            "WindowsAzure.Caching",
            "WindowsAzure.ServiceBus",
            "WindowsAzure.Storage"
        };

        [Fact]
        public void ReturnsCorrectReferences()
        {
            // Arrange
            var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream("NuGet.Jobs.GitHubIndexer.Tests.NuGetGallery.csproj.test");

            var utils = new RepoUtils(Mock.Of<ILogger<RepoUtils>>());
            
            // Act
            var references = utils.ParseProjFile(stream, "repo");

            // Assert
            Assert.Equal(
                OrderReferences(_expectedReferences), 
                OrderReferences(references));
        }

        private static IOrderedEnumerable<string> OrderReferences(IReadOnlyList<string> references)
        {
            return references.OrderBy(r => r);
        }
    }
}
