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
        public HttpPostedFileBase Symbols { get; set; }

        /// <summary>
        /// Gets or sets the group for the shared package and symbol identity.
        /// </summary>
        [StringLength(64)]
        [RegularExpression(@"^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$")]
        public string GroupId { get; set; }
    }
}
