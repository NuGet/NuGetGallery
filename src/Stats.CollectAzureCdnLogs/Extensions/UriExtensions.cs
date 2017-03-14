// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// ReSharper disable once CheckNamespace
namespace System
{
    public static class UriExtensions
    {
        private const string _slashCharacter = "/";

        public static Uri EnsureTrailingSlash(this Uri uri)
        {
            var uriString = uri.ToString();
            if (!uriString.EndsWith(_slashCharacter, StringComparison.Ordinal))
            {
                return new Uri(uriString + _slashCharacter);
            }
            return uri;
        }
    }
}