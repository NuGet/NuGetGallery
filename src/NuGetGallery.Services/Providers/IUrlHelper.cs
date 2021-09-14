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

        /// <summary>
        /// Produces a URL to the package ownership request confirmation page.
        /// </summary>
        /// <param name="id">The package ID for the ownership request.</param>
        /// <param name="username">The username of the ownership request recipient (new owner).</param>
        /// <param name="confirmationCode">The confirmation code (secret) associated with the request.</param>
        /// <param name="relativeUrl">True to return a relative URL, false to return an absolute URL.</param>
        /// <returns>The relative or absolute URL as a string.</returns>
        string ConfirmPendingOwnershipRequest(string id, string username, string confirmationCode, bool relativeUrl);

        /// <summary>
        /// Produces a URL to the package ownership request rejection page.
        /// </summary>
        /// <param name="id">The package ID for the ownership request.</param>
        /// <param name="username">The username of the ownership request recipient (new owner).</param>
        /// <param name="confirmationCode">The confirmation code (secret) associated with the request.</param>
        /// <param name="relativeUrl">True to return a relative URL, false to return an absolute URL.</param>
        /// <returns>The relative or absolute URL as a string.</returns>
        string RejectPendingOwnershipRequest(string id, string username, string confirmationCode, bool relativeUrl);

        /// <summary>
        /// Produces a URL to manage the ownership of an existing package.
        /// </summary>
        /// <param name="id">The package ID to manage ownership for.</param>
        /// <param name="relativeUrl">True to return a relative URL, false to return an absolute URL.</param>
        /// <returns>The relative or absolute URL as a string.</returns>
        string ManagePackageOwnership(string id, bool relativeUrl);
    }
}
