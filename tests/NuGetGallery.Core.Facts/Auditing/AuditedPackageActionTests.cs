// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditedPackageActionTests : EnumTests
    {
        [Fact]
        public void Definition_HasNotChanged()
        {
            var expectedNames = new[]
            {
                "Create",
                "Delete",
                "Edit",
                "List",
                "SoftDelete",
                "UndoEdit",
                "Unlist",
                "Verify"
            };

            Verify(typeof(AuditedPackageAction), expectedNames);
        }
    }
}