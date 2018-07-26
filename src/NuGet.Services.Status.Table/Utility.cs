// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status.Table
{
    internal static class Utility
    {
        public static string ToRowKeySafeComponentPath(string componentPath)
        {
            return componentPath.Replace(Constants.ComponentPathDivider, '_');
        }
    }
}
