// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsServiceFacts
    {
        public class TheGetSourceMethod
        {
            [Fact]
            public void RequiresNonNullOrEmptyName()
            {
                ContractAssert.ThrowsArgNullOrEmpty(s => new DiagnosticsService().GetSource(s), "name");
            }
        }
    }
}
