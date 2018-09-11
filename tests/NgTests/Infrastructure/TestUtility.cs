// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NgTests.Infrastructure
{
    public static class TestUtility
    {
        private static readonly Random _random = new Random();

        public static string CreateRandomAlphanumericString()
        {
            return CreateRandomAlphanumericString(_random);
        }

        public static string CreateRandomAlphanumericString(Random random)
        {
            const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(
                Enumerable.Repeat(characters, count: 16)
                    .Select(s => s[random.Next(s.Length)])
                    .ToArray());
        }
    }
}