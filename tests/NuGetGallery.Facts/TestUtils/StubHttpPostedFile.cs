// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Web;

namespace NuGetGallery
{
    internal sealed class StubHttpPostedFile : HttpPostedFileBase
    {
        public override int ContentLength { get; }
        public override string ContentType { get; }
        public override string FileName { get; }
        public override Stream InputStream { get; }

        internal StubHttpPostedFile(int contentLength, string fileName, Stream inputStream)
        {
            ContentLength = contentLength;
            FileName = fileName;
            InputStream = inputStream;
        }
    }
}