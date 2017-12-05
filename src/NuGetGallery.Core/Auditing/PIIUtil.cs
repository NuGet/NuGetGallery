// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class PIIUtil
    {
        public static string ObfuscateIp(string IP)
        {
            if (!string.IsNullOrEmpty(IP) && IP.IndexOf(".", StringComparison.Ordinal) > 0)
            {
                return IP.Substring(0, IP.LastIndexOf(".", StringComparison.Ordinal)) + ".0";
            }
            return IP;
        }
    }
}
