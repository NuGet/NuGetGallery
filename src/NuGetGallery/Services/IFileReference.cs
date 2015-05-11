// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public interface IFileReference
    {
        /// <summary>
        /// Gets the content ID suitable for use in the ifNoneMatch parameter of <see cref="IFileStorageService.GetFileReferenceAsync"/>
        /// </summary>
        string ContentId { get; }
        
        Stream OpenRead();
    }
}
