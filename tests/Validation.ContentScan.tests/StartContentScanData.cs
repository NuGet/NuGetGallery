// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Jobs.Validation.ContentScan;
using Xunit;

namespace Validation.ContentScan.Tests
{
    public class StartContentScanDataFacts
    {
        
        [Fact]
        public void ConstructorThrowWhenBlobUrlIsNullForScanOperation()
        {
            var ex = Record.Exception(() => new StartContentScanData(
                Guid.NewGuid(),null));

            Assert.NotNull(ex);
        }
    }
}
