// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class ReadMeRequest
    {
        public virtual HttpPostedFileBase ReadMeFile { get; set; }

        [AllowHtml]
        public virtual string ReadMeWritten { get; set; }

        public virtual string ReadMeUrl { get; set; }

        public virtual string ReadMeType { get; set; }

        public PackageEditReadMeState ReadMeState { get; set; }
        public bool Overwriting { get; set; }
    }
}