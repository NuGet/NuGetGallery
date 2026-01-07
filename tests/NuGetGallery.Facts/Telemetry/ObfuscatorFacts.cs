// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class ObfuscatorFacts
    {
        [Fact]
        public void ObfuscatedActionsAreNotCaseSensitive()
        {
            // Arange
            var obfuscatedOperations = Obfuscator.ObfuscatedActions;
            var lowerInvariant = obfuscatedOperations.Select(a => a.ToLowerInvariant());
            var upperInvariant = obfuscatedOperations.Select(a => a.ToUpperInvariant());

            // Act and Assert
            foreach (var action in lowerInvariant)
            {
                Assert.Contains(action, Obfuscator.ObfuscatedActions);
            }
            foreach (var action in upperInvariant)
            {
                Assert.Contains(action, Obfuscator.ObfuscatedActions);
            }
        }
    }
}
