// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Indexing
{
    public static class UriFormatter
    {
        public static string MakeRegistrationRelativeAddress(string id)
        {
            return string.Format("{0}/index.json", id).ToLowerInvariant();
        }

        public static string MakePackageRelativeAddress(string id, string version)
        {
            return string.Format("{0}/{1}.json", id, version).ToLowerInvariant();
        }
    }
}
