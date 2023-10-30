// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using NuGet.Services.Entities;
using NuGetGallery.Cookies;
using Microsoft.Extensions.Logging;

namespace NuGetGallery
{
    public static class TyposquattingService
    {
        public static ITyposquattingService Instance;
        public static ILogger Logger;

        public static void Initialize(ITyposquattingService typosquattingService, ILogger logger)
        {
            Instance = typosquattingService ?? throw new ArgumentNullException(nameof(typosquattingService));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }
}