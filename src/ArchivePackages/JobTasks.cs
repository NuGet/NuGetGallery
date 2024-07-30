// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace ArchivePackages
{
    public static class JobTasks
    {
        public const EventTask GatheringDbPackages = (EventTask)0x1;

        public const EventTask ArchivingPackages = (EventTask)0x2;

        public const EventTask StartingPackageCopy = (EventTask)0x3;
    }
}
