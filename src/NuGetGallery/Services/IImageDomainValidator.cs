// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IImageDomainValidator
    {
        /// <summary>
        /// If the input uri is http => check if it's a trusted domain and convert to https.
        /// If the input uri is https => check if it's a trusted domain 
        /// If the input uri is not a valid uri or not http/https => return false
        /// </summary>
        bool TryPrepareImageUrlForRendering(string uriString, out string readyUriString);
    }
}
