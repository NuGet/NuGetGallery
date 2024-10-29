// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;

namespace NuGetGallery
{
    public sealed class CertificateFile : IDisposable
    {
        private static readonly byte[] EmptyBuffer = Array.Empty<byte>();

        public string Sha1Thumbprint { get; }
        public string Sha256Thumbprint { get; }
        public Stream Stream { get; }

        private CertificateFile(Stream stream, string sha1Thumbprint, string sha256Thumbprint)
        {
            Stream = stream;
            Sha1Thumbprint = sha1Thumbprint;
            Sha256Thumbprint = sha256Thumbprint;
        }

        public void Dispose()
        {
            Stream.Dispose();
        }

        public static CertificateFile Create(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var readOnlyStream = CopyAsReadOnly(stream);

            var sha1Thumbprint = GetSha1Thumbprint(readOnlyStream);
            var thumbprint = GetSha256Thumbprint(readOnlyStream);

            readOnlyStream.Position = 0;

            return new CertificateFile(readOnlyStream, sha1Thumbprint, thumbprint);
        }

        private static MemoryStream CopyAsReadOnly(Stream source)
        {
            var capacity = (int)source.Length;

            using (var destination = new MemoryStream(capacity: capacity))
            {
                source.Position = 0;
                source.CopyTo(destination);

                return new MemoryStream(
                    destination.GetBuffer(),
                    index: 0,
                    count: capacity,
                    writable: false,
                    publiclyVisible: true);
            }
        }

        private static string GetSha1Thumbprint(MemoryStream stream)
        {
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            using (var hashAlgorithm = SHA1.Create())
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
            {
                return GetThumbprint(stream, hashAlgorithm);
            }
        }

        private static string GetSha256Thumbprint(MemoryStream stream)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                return GetThumbprint(stream, hashAlgorithm);
            }
        }

        private static string GetThumbprint(MemoryStream stream, HashAlgorithm hashAlgorithm)
        {
            var buffer = stream.GetBuffer();

            hashAlgorithm.TransformBlock(buffer, 0, buffer.Length, outputBuffer: null, outputOffset: 0);
            hashAlgorithm.TransformFinalBlock(EmptyBuffer, inputOffset: 0, inputCount: 0);

            return GetHexString(hashAlgorithm.Hash);
        }

        private static string GetHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}