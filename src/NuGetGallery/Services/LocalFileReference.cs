// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class LocalFileReference : IFileReference
    {
        private FileInfo _file;

        public string ContentId
        {
            get { return _file.LastWriteTimeUtc.ToString("O", CultureInfo.CurrentCulture); }
        }

        public LocalFileReference(FileInfo file)
        {
            _file = file;
        }

        public Stream OpenRead()
        {
            return _file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
    }
}