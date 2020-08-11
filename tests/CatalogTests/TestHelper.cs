// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Xunit;

namespace CatalogTests
{
    internal static class TestHelper
    {
        internal static MemoryStream GetStream(string fileName)
        {
            var path = Path.GetFullPath(Path.Combine("TestData", fileName));

            // Multiple tests may try reading the file concurrently.
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bytes = new byte[stream.Length];

                Assert.Equal(bytes.Length, stream.Read(bytes, offset: 0, count: bytes.Length));

                return new MemoryStream(bytes, index: 0, count: bytes.Length, writable: false);
            }
        }
    }
}