// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public static class LogEvents
    {
        public static EventId ValidationFailed = new EventId(900, "Validation failed!");
        public static EventId ValidationFailedToRun = new EventId(901, "Failed to run validation!");
        public static EventId ValidationFailedToInitialize = new EventId(902, "Failed to initialize validation!");
    }
}
