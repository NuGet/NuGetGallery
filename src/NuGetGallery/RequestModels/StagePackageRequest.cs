// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Describes a multipart package staging upload.
    /// </summary>
    public class StagePackageRequest
    {
        /// <summary>
        /// Gets or sets the package file to stage.
        /// </summary>
        [Required]
        public HttpPostedFileBase Package { get; set; }

        /// <summary>
        /// Gets or sets the existing group to assign to the package identity.
        /// </summary>
        [StringLength(64)]
        [RegularExpression(@"^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$")]
        public string GroupId { get; set; }

        /// <summary>
        /// Gets or sets the package's listing intent.
        /// </summary>
        public bool? Listed { get; set; }
    }
}
