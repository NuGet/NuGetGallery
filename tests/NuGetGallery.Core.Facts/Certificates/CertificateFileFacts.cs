// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Xunit;

namespace NuGetGallery.Certificates
{
    public class CertificateFileFacts
    {
        [Fact]
        public void Create_WhenStreamIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => CertificateFile.Create(stream: null));

            Assert.Equal("stream", exception.ParamName);
        }

        [Fact]
        public void Create_CreatesReadOnlyCopyOfStream()
        {
            using (var stream = new MemoryStream())
            {
                var file = CertificateFile.Create(stream);

                Assert.False(file.Stream.CanWrite);
                Assert.Equal(0, file.Stream.Position);
                Assert.NotSame(stream, file.Stream);
            }
        }

        [Fact]
        public void Create_HashesEmptyStream()
        {
            using (var stream = new MemoryStream())
            {
                var file = CertificateFile.Create(stream);

                Assert.Equal("da39a3ee5e6b4b0d3255bfef95601890afd80709", file.Sha1Thumbprint);
                Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", file.Sha256Thumbprint);
            }
        }

        [Fact]
        public void Create_HashesStream()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc")))
            {
                var file = CertificateFile.Create(stream);

                Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", file.Sha1Thumbprint);
                Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", file.Sha256Thumbprint);
            }
        }
    }
}