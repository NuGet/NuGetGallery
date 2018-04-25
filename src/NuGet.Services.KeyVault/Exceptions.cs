// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.KeyVault
{
    internal static class Exceptions
    {
        private const string ArgumentNullOrEmptyMessage = "Value cannot be null or empty.";

        public static ArgumentException ArgumentNullOrEmpty(string paramName)
        {
            return new ArgumentException(ArgumentNullOrEmptyMessage, paramName);
        }
    }
}
