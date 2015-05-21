// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Linq;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class PackageIndexEntityFacts
    {
        [Theory]
        [InlineData("NHibernate", new [] { "NHibernate" })]
        [InlineData("NUnit", new [] { "NUnit" })]
        [InlineData("EntityFramework", new [] { "EntityFramework", "Framework", "Entity" })]
        [InlineData("Sys-netFX", new [] { "Sys-netFX", "Sys", "netFX" })]
        [InlineData("xUnit", new [] { "xUnit" })]
        [InlineData("jQueryUI", new [] { "jQueryUI" })]
        [InlineData("jQuery-UI", new [] { "jQuery-UI", "jQuery", "UI" })]
        [InlineData("NuGetPowerTools", new [] { "NuGetPowerTools", "NuGet", "Power", "Tools" })]
        [InlineData("microsoft-web-helpers", new [] { "microsoft-web-helpers", "microsoft", "web", "helpers" })]
        [InlineData("EntityFramework.sample", new [] { "EntityFramework.sample", "EntityFramework", "sample", "Framework", "Entity" })]
        [InlineData("SignalR.MicroSliver", new [] { "SignalR.MicroSliver", "SignalR", "MicroSliver", "Micro", "Sliver" })]
        [InlineData("ABCMicroFramework", new [] { "ABCMicroFramework", "ABC", "Micro", "Framework" })]
        [InlineData("SignalR.Hosting.AspNet", new [] { "SignalR.Hosting.AspNet", "SignalR", "Hosting", "AspNet", "Asp", "Net" })]
        public void CamelCaseTokenizer(string term, string[] tokens)
        {
            // Act
            var result = PackageIndexEntity.TokenizeId(term);

            // Assert
            Assert.Equal(tokens.OrderBy(p => p), result.OrderBy(p => p));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData(".", "")]
        [InlineData("JQuery", "JQuery")]
        [InlineData("JQuery-UI", "JQuery UI")]
        [InlineData("JQuery.UI.Combined", "JQuery UI Combined")]
        public void IdSplitter(string term, string tokens)
        {
            var result = PackageIndexEntity.SplitId(term);
            Assert.Equal(result, tokens);
        }

        [InlineData("Sys-netFX", "Sys netFX")]
        [InlineData("xUnit", "xUnit")]
        [InlineData("jQueryUI", "jQueryUI")]
        [InlineData("jQuery-UI", "jQuery UI")]
        [InlineData("NuGetPowerTools", "NuGet Power Tools")]
        [InlineData("microsoft-web-helpers", "microsoft web helpers" )]
        [InlineData("EntityFramework.sample", "Entity Framework sample" )]
        [InlineData("SignalR.MicroSliver", "SignalR Micro Sliver")]
        [InlineData("ABCMicroFramework", "ABC Micro Framework")]
        [InlineData("SignalR.Hosting.AspNet", "SignalR Hosting Asp Net")]
        public void CamelIdSplitter(string term, string tokens)
        {
            var result = PackageIndexEntity.CamelSplitId(term);
            Assert.Equal(result, tokens);
        }
    }
}