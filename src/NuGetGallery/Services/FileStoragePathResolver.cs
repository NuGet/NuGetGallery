// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Hosting;

namespace NuGetGallery
{
    public static class FileStoragePathResolver
    {
        public static string Resolve(string fileStorageDirectory)
        {
            if (fileStorageDirectory.StartsWith("~/", StringComparison.OrdinalIgnoreCase) && HostingEnvironment.IsHosted)
            {
                return HostingEnvironment.MapPath(fileStorageDirectory);
            }

            return fileStorageDirectory;
        }
    }
}
