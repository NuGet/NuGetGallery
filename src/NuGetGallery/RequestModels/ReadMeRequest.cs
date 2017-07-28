﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class ReadMeRequest
    {
        public virtual HttpPostedFileBase ReadMeFile { get; set; }
        public virtual string ReadMeWritten { get; set; }
        public virtual string ReadMeUrl { get; set; }
        public virtual string ReadMeType { get; set; }
        public PackageEditReadMeState ReadMeState { get; set; }
    }
}