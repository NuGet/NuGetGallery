// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace ArchivePackages
{
    [EventSource(Name = "Outercurve-NuGet-Jobs-ArchivePackages")]
    public class JobEventSource : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();

        private JobEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Preparing to archive packages from {0}/{1} to primary destination {2}/{3} using package data from {4}/{5}")]
        public void PreparingToArchive(string sourceAccount, string sourceContainer, string destAccount, string destContainer, string dbServer, string dbName) { WriteEvent(1, sourceAccount, sourceContainer, destAccount, destContainer, dbServer, dbName); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Message = "Preparing to archive packages to secondary destination {0}/{1}")]
        public void PreparingToArchive2(string destAccount, string destContainer) { WriteEvent(2, destAccount, destContainer); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Message = "Cursor data: CursorDateTime is {0}")]
        public void CursorData(string cursorDateTime) { WriteEvent(3, cursorDateTime); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Task = JobTasks.GatheringDbPackages,
            Opcode = EventOpcode.Start,
            Message = "Gathering list of packages to archive from {0}/{1}")]
        public void GatheringPackagesToArchiveFromDb(string dbServer, string dbName) { WriteEvent(4, dbServer, dbName); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = JobTasks.GatheringDbPackages,
            Opcode = EventOpcode.Stop,
            Message = "Gathered {0} packages to archive from {1}/{2}")]
        public void GatheredPackagesToArchiveFromDb(int gathered, string dbServer, string dbName) { WriteEvent(5, gathered, dbServer, dbName); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Task = JobTasks.ArchivingPackages,
            Opcode = EventOpcode.Start,
            Message = "Starting archive of {0} packages.")]
        public void StartingArchive(int count) { WriteEvent(6, count); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = JobTasks.ArchivingPackages,
            Opcode = EventOpcode.Stop,
            Message = "Started archive.")]
        public void StartedArchive() { WriteEvent(7); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Message = "Archive already exists: {0}")]
        public void ArchiveExists(string blobName) { WriteEvent(8, blobName); }

        [Event(
            eventId: 9,
            Level = EventLevel.Warning,
            Message = "Source Blob does not exist: {0}")]
        public void SourceBlobMissing(string blobName) { WriteEvent(9, blobName); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Task = JobTasks.StartingPackageCopy,
            Opcode = EventOpcode.Start,
            Message = "Starting copy of {0} to {1}.")]
        public void StartingCopy(string source, string dest) { WriteEvent(12, source, dest); }

        [Event(
            eventId: 13,
            Level = EventLevel.Informational,
            Task = JobTasks.StartingPackageCopy,
            Opcode = EventOpcode.Stop,
            Message = "Started copy of {0} to {1}.")]
        public void StartedCopy(string source, string dest) { WriteEvent(13, source, dest); }

        [Event(
            eventId: 14,
            Level = EventLevel.Informational,
            Message = "NewCursor data: CursorDateTime is {0}")]
        public void NewCursorData(string cursorDateTime) { WriteEvent(14, cursorDateTime); }
    }
}