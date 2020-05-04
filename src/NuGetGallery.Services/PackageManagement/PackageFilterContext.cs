// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Routing;

namespace NuGetGallery.Services
{
    public class PackageFilterContext
    {
        public RouteBase RouteBase { get; }
            
        public string Version { get; }

        public PackageFilterContext(RouteBase routeBase, string version)
        {
            RouteBase = routeBase;
            Version = version;
        }
    }
}