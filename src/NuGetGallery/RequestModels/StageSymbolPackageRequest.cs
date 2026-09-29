// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Describes a multipart staged symbol upload.
    /// </summary>
    public class StageSymbolPackageRequest
    {
        /// <summary>
        /// Gets or sets the symbol package file to stage.
        /// </summary>
        [Required]
        public HttpPostedFileBase Package { get; set; }
    }
}
