// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Helpers;

namespace NuGetGallery.Services
{
    public class TestFileReference : IFileReference
    {
        private byte[] _content;
        private int _openCount = 0;

        public int OpenCount { get { return _openCount; } }

        public string ContentId
        {
            get;
            private set;
        }

        public TestFileReference(byte[] content, string contentId)
        {
            _content = content;
            ContentId = contentId;
        }

        public static TestFileReference Create(string content)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            string hash = Crypto.Hash(contentBytes);
            return new TestFileReference(contentBytes, hash);
        }

        public Stream OpenRead()
        {
            Interlocked.Increment(ref _openCount);
            return new MemoryStream(_content);
        }
    }
}
