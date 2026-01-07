// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;

namespace NuGetGallery.Services
{
    public class CertificateValidatorFacts
    {
        private const int MaximumSizeInBytes = 10000;

        [Fact]
        public void Validate_WhenFileIsNull_Throws()
        {
            var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file: null));

            Assert.Equal("file", ((ArgumentNullException)exception.InnerException).ParamName);
            Assert.Equal("A certificate file is required.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(".")]
        [InlineData("a.crt")]
        [InlineData("a.pfx")]
        [InlineData(".pfx")]
        public void Validate_WhenFileExtensionIsInvalid_Throws(string fileName)
        {
            var file = new StubHttpPostedFile(contentLength: 1024, fileName: fileName, inputStream: Stream.Null);
            var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

            Assert.Equal("The file extension must be .cer.", exception.Message);
        }

        [Fact]
        public void Validate_WhenStreamIsNull_Throws()
        {
            var file = new StubHttpPostedFile(contentLength: 1024, fileName: "a.cer", inputStream: null);
            var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

            Assert.Equal("The file stream is invalid.", exception.Message);
        }

        [Fact]
        public void Validate_WhenStreamIsNotSeekable_Throws()
        {
            using (var stream = new NonSeekableStream())
            {
                var file = new StubHttpPostedFile(contentLength: 1024, fileName: "a.cer", inputStream: stream);
                var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

                Assert.Equal("The file stream must be seekable.", exception.Message);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void Validate_WhenContentLengthIsTooSmall_Throws(int contentLength)
        {
            var file = new StubHttpPostedFile(contentLength, "a.cer", Stream.Null);
            var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

            Assert.Equal("The file length is invalid.", exception.Message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void Validate_WhenStreamLengthIsTooSmall_Throws(long streamLength)
        {
            using (var stream = new CustomLengthStream(streamLength))
            {
                var file = new StubHttpPostedFile(contentLength: 1024, fileName: "a.cer", inputStream: stream);
                var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

                Assert.Equal("The file length is invalid.", exception.Message);
            }
        }

        [Fact]
        public void Validate_WhenContentLengthIsTooLarge_Throws()
        {
            using (var stream = GetDerEncodedCertificateStream())
            {
                var file = new StubHttpPostedFile(contentLength: MaximumSizeInBytes + 1, fileName: "a.cer", inputStream: stream);
                var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

                Assert.Equal($"The file exceeds the size limit of {MaximumSizeInBytes} bytes.", exception.Message);
            }
        }

        [Fact]
        public void Validate_WhenStreamLengthIsTooLarge_Throws()
        {
            using (var stream = new CustomLengthStream(MaximumSizeInBytes + 1))
            {
                var file = new StubHttpPostedFile(contentLength: 1024, fileName: "a.cer", inputStream: stream);
                var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

                Assert.Equal($"The file exceeds the size limit of {MaximumSizeInBytes} bytes.", exception.Message);
            }
        }

        [Fact]
        public void Validate_WhenStreamIsPfx_Throws()
        {
            using (var stream = GetPfxStream())
            {
                var file = new StubHttpPostedFile((int)stream.Length, fileName: "a.cer", inputStream: stream);
                var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

                Assert.Equal("The file must be a DER encoded binary X.509 certificate.", exception.Message);
            }
        }

        [Fact]
        public void Validate_WhenStreamIsPemEncodedCertificate_Throws()
        {
            using (var stream = GetPemEncodedCertificateStream())
            {
                var file = new StubHttpPostedFile((int)stream.Length, fileName: "a.cer", inputStream: stream);
                var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

                Assert.Equal("The file must be a DER encoded binary X.509 certificate.", exception.Message);
            }
        }

        [Fact]
        public void Validate_WhenStreamIsDerEncodingIsMalformedShortFormLength_Throws()
        {
            var bytes = new byte[3];

            bytes[0] = 0x30;  // constructed sequence
            bytes[1] = 0x02;  // short form length

            // The DER encoding says there's 2 bytes of content but the array only has 1 remaining byte.

            using (var stream = new MemoryStream(bytes, writable: false))
            {
                var file = new StubHttpPostedFile((int)stream.Length, fileName: "a.cer", inputStream: stream);
                var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

                Assert.Equal("The file must be a DER encoded binary X.509 certificate.", exception.Message);
            }
        }

        [Fact]
        public void Validate_WhenStreamIsDerEncodingIsMalformedLongFormLength_Throws()
        {
            var bytes = new byte[4];

            bytes[0] = 0x30;  // constructed sequence
            bytes[1] = 0x81;  // long form length
            bytes[2] = 0x01;  // 256

            // The DER encoding says there's 256 bytes of content but the array only has 1 remaining byte.

            using (var stream = new MemoryStream(bytes, writable: false))
            {
                var file = new StubHttpPostedFile((int)stream.Length, fileName: "a.cer", inputStream: stream);
                var exception = Assert.Throws<UserSafeException>(() => new CertificateValidator().Validate(file));

                Assert.Equal("The file must be a DER encoded binary X.509 certificate.", exception.Message);
            }
        }

        [Theory]
        [InlineData("a.cer")]
        [InlineData("A.CER")]
        public void Validate_WhenStreamIsDerEncodedCertificate_Succeeds(string fileName)
        {
            using (var stream = GetDerEncodedCertificateStream())
            {
                var file = new StubHttpPostedFile((int)stream.Length, fileName, stream);

                new CertificateValidator().Validate(file);
            }
        }

        private X509Certificate2 GetCertificate()
        {
            return new X509Certificate2(TestDataResourceUtility.GetResourceBytes("certificate.cer"));
        }

        private MemoryStream GetDerEncodedCertificateStream()
        {
            using (var certificate = GetCertificate())
            {
                return new MemoryStream(certificate.RawData, writable: false);
            }
        }

        private MemoryStream GetPfxStream()
        {
            var builder = new StringBuilder();

            builder.AppendLine("-----BEGIN CERTIFICATE-----");

            using (var certificate = GetCertificate())
            {
                builder.AppendLine(Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks));
            }

            builder.AppendLine("-----END CERTIFICATE-----");

            var pem = builder.ToString();

            return new MemoryStream(Encoding.UTF8.GetBytes(pem), writable: false);
        }

        private MemoryStream GetPemEncodedCertificateStream()
        {
            using (var certificate = GetCertificate())
            {
                return new MemoryStream(certificate.Export(X509ContentType.Pfx), writable: false);
            }
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            public override bool CanSeek => false;
        }

        private sealed class CustomLengthStream : MemoryStream
        {
            public override long Length { get; }

            internal CustomLengthStream(long length)
            {
                Length = length;
            }
        }
    }
}