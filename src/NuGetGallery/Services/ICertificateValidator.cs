// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a certificate validator.
    /// </summary>
    public interface ICertificateValidator
    {
        /// <summary>
        /// Validates a certificate.
        /// </summary>
        /// <param name="file">A certificate file.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="file" /> is <c>null</c>.</exception>
        void Validate(HttpPostedFileBase file);
    }
}