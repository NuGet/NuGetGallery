// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.RequestModels
{
    public class ReadMeRequest
    {
        public HttpPostedFileBase ReadMeFile { get; set; }
        public string ReadMeWritten { get; set; }
        public string ReadMeUrl { get; set; }
        public String ReadMeType { get; set; }
    }
}