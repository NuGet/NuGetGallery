// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class AppConfigIsCorrectlyApplied
    {
        [Fact]
        public void VerifyAppDomainHasConfigurationSettings()
        {
            string value = ConfigurationManager.AppSettings["YourTestsAreNotGoingInsane"];
            Assert.False(String.IsNullOrEmpty(value), "App.Config not loaded");
        }

        [Fact]
        public void VerifyBindingRedirectToMvc4IsWorking()
        {
            // System.Web.Mvc should be binding redirected from version 3.0.0.0 to 4.0.0.1 just like in our actual app.
            string typeName = "System.Web.Mvc.Controller, System.Web.Mvc, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
            Type resolvedType = Type.GetType(typeName);
            Version runtimeVersion = resolvedType.Assembly.GetName().Version;
            Assert.Equal(new Version("4.0.0.1"), runtimeVersion);
        }
    }
}
