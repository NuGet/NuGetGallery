// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public static class HexConverter
    {
        public static byte[] ToByteArray(string? hex)
        {
            if (hex is null)
            {
                return Array.Empty<byte>();
            }

            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
}
