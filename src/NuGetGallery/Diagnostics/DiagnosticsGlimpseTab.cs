// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Glimpse.Core.Extensibility;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsGlimpseTab : TabBase
    {
        public override object GetData(ITabContext context)
        {
            return new
            {
                MessageQueues = MessageQueue.GetQueueStats()
            };
        }

        public override string Name
        {
            get { return "Diag"; }
        }
    }
}