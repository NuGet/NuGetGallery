// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class ConfigurationAttributeFacts
    {
        [Fact]
        public void ConfigurationKeyAttributeThrowsWhenKeyNull()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationKeyAttribute(null));
        }

        [Fact]
        public void ConfigurationKeyPrefixAttributeThrowsWhenPrefixNull()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationKeyPrefixAttribute(null));
        }

        [Fact]
        public void ConfigurationKeyAttributeThrowsWhenKeyEmpty()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationKeyAttribute(""));
        }

        [Fact]
        public void ConfigurationKeyPrefixAttributeThrowsWhenPrefixEmpty()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationKeyPrefixAttribute(""));
        }
    }
}
