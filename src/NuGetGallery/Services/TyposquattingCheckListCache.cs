// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class TyposquattingCheckListCache
    {
        public object Locker { get; } = new object();
        public List<string> Cache { get; set; }
        public DateTime LastRefreshTime { get; set; }
        public TimeSpan DefaultExpireTime { get; set; }
    }
}