// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;

namespace NuGetGallery
{
    /// <summary>
    /// Provides a way for the service layer (NuGetGallery.Services) to produce URLs. In an ASP.NET MVC web project
    /// this would be implemented by wrapping <see cref="UrlHelper"/> and an extension methods on that type.
    /// </summary>
    public interface IUrlHelper
    {
        /// <summary>
        /// Produces a URL to the package details (a.k.a. display package) page given a package ID and optional version.
        /// </summary>
        /// <param name="id">The package ID to link to.</param>
        /// <param name="version">The specific package version to link to. Can be null.</param>
        /// <param name="relativeUrl">True to return a relative URL, false to return an absolute URL.</param>
        /// <returns>The relative or absolute URL as a string.</returns>
        string Package(string id, string version, bool relativeUrl);
    }
}
