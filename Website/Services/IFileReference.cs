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
        /// Returns true if the file has changed from the version specified in the ifNoneMatch parameter of <see cref="IFileStorageService.GetFileReferenceAsync"/>
        /// </summary>
        bool HasChanged { get; }

        /// <summary>
        /// Gets the content ID suitable for use in the ifNoneMatch parameter of <see cref="IFileStorageService.GetFileReferenceAsync"/>
        /// </summary>
        string ContentId { get; }
        
        Stream OpenRead();
    }
}
