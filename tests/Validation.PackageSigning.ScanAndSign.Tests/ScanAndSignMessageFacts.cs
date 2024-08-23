// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Jobs.Validation.ScanAndSign;
using Xunit;

namespace Validation.PackageSigning.ScanAndSign.Tests
{
    public class ScanAndSignMessageFacts
    {
        [Fact]
        public void ConstructorDoesNotThrowWhenIndexUrlIsNullForScanOperation()
        {
            var ex = Record.Exception(() => new ScanAndSignMessage(
                OperationRequestType.Scan,
                Guid.NewGuid(),
                new Uri("https://example.com/aaa.nupkg"),
                v3ServiceIndexUrl: null,
                owners: new List<string> { "owner1" },
                context: new Dictionary<string, string>()));

            Assert.Null(ex);
        }

        [Fact]
        public void ConstructorDoesNotThrowWhenOwnersIsNullForScanOperation()
        {
            var ex = Record.Exception(() => new ScanAndSignMessage(
                OperationRequestType.Scan,
                Guid.NewGuid(),
                new Uri("https://example.com/aaa.nupkg"),
                v3ServiceIndexUrl: "https://example.com/index.json",
                owners: null,
                context: new Dictionary<string, string>()));

            Assert.Null(ex);
        }

        [Fact]
        public void ShortConstructorCannotBeUsedForSignOperation()
        {
            var ex = Assert.Throws<ArgumentException>(() => new ScanAndSignMessage(
                OperationRequestType.Sign,
                Guid.NewGuid(),
                new Uri("https://example.com/aaa.nupkg"),
                new Dictionary<string, string>()));

            Assert.Contains(nameof(OperationRequestType.Sign), ex.Message);
        }
    }
}
