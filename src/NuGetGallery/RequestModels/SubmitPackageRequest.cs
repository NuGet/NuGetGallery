// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace NuGetGallery
{
    public class SubmitPackageRequest
    {
        [Required]
        [Hint("Your package file will be uploaded and hosted on the gallery server.")]
        public HttpPostedFile PackageFile { get; set; }
    }
}