// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Stats.ImportAzureCdnStatistics
{
    [EventSource(Name = "NetFoundation-NuGet-Jobs-ImportAzureCdnStatistics")]
    public class JobEventSource
        : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();

        private JobEventSource()
        {
        }

        [Event(eventId: 1, Level = EventLevel.Informational, Message = "Beginning blob listing using prefix {0}", Task = Tasks.BlobListing, Opcode = EventOpcode.Start)]
        public void BeginningBlobListing(string prefix)
        {
            WriteEvent(1, prefix);
        }

        [Event(eventId: 2, Level = EventLevel.Informational, Message = "Finishing blob listing using prefix {0}", Task = Tasks.BlobListing, Opcode = EventOpcode.Stop)]
        public void FinishingBlobListing(string prefix)
        {
            WriteEvent(2, prefix);
        }

        [Event(eventId: 3, Level = EventLevel.Warning, Message = "Failed blob listing using prefix {0}", Task = Tasks.BlobListing, Opcode = EventOpcode.Suspend)]
        public void FailedBlobListing(string prefix)
        {
            WriteEvent(3, prefix);
        }

        [Event(eventId: 4, Level = EventLevel.Informational, Message = "Beginning acquiring lease for blob {0}", Task = Tasks.AcquireLease, Opcode = EventOpcode.Start)]
        public void BeginningAcquireLease(string uri)
        {
            WriteEvent(4, uri);
        }

        [Event(eventId: 5, Level = EventLevel.Informational, Message = "Finished acquiring lease for blob {0}", Task = Tasks.AcquireLease, Opcode = EventOpcode.Stop)]
        public void FinishedAcquireLease(string uri)
        {
            WriteEvent(5, uri);
        }

        [Event(eventId: 6, Level = EventLevel.Error, Message = "Failed to acquire lease for blob {0}", Task = Tasks.AcquireLease, Opcode = EventOpcode.Suspend)]
        public void FailedAcquireLease(string uri)
        {
            WriteEvent(6, uri);
        }

        [Event(eventId: 7, Level = EventLevel.Informational, Message = "Beginning opening of compressed blob {0}", Task = Tasks.DecompressBlob, Opcode = EventOpcode.Start)]
        public void BeginningOpenCompressedBlob(string uri)
        {
            WriteEvent(7, uri);
        }

        [Event(eventId: 8, Level = EventLevel.Informational, Message = "Finished opening of compressed blob {0}", Task = Tasks.DecompressBlob, Opcode = EventOpcode.Stop)]
        public void FinishedOpenCompressedBlob(string uri)
        {
            WriteEvent(8, uri);
        }

        [Event(eventId: 9, Level = EventLevel.Error, Message = "Failed to open compressed blob {0}", Task = Tasks.DecompressBlob, Opcode = EventOpcode.Suspend)]
        public void FailedOpenCompressedBlob(string uri)
        {
            WriteEvent(9, uri);
        }

        [Event(eventId: 10, Level = EventLevel.Informational, Message = "Beginning to parse blob {0}", Task = Tasks.ParseLog, Opcode = EventOpcode.Start)]
        public void BeginningParseLog(string uri)
        {
            WriteEvent(10, uri);
        }

        [Event(eventId: 11, Level = EventLevel.Informational, Message = "Finished to parse blob {0} ({1} records)", Task = Tasks.ParseLog, Opcode = EventOpcode.Stop)]
        public void FinishingParseLog(string uri, int recordCount)
        {
            WriteEvent(11, uri, recordCount);
        }

        [Event(eventId: 12, Level = EventLevel.Error, Message = "Failed to parse blob {0}", Task = Tasks.ParseLog, Opcode = EventOpcode.Suspend)]
        public void FailedParseLog(string uri)
        {
            WriteEvent(12, uri);
        }

        [Event(eventId: 13, Level = EventLevel.Informational, Message = "Beginning archive upload for blob {0}", Task = Tasks.UploadArchive, Opcode = EventOpcode.Start)]
        public void BeginningArchiveUpload(string uri)
        {
            WriteEvent(13, uri);
        }

        [Event(eventId: 14, Level = EventLevel.Informational, Message = "Finished archive upload for blob {0}", Task = Tasks.UploadArchive, Opcode = EventOpcode.Stop)]
        public void FinishingArchiveUpload(string uri)
        {
            WriteEvent(14, uri);
        }

        [Event(eventId: 15, Level = EventLevel.Error, Message = "Failed archive upload for blob {0}", Task = Tasks.UploadArchive, Opcode = EventOpcode.Suspend)]
        public void FailedArchiveUpload(string uri)
        {
            WriteEvent(15, uri);
        }

        [Event(eventId: 16, Level = EventLevel.Informational, Message = "Beginning to delete blob {0}", Task = Tasks.DeleteBlob, Opcode = EventOpcode.Start)]
        public void BeginningDelete(string uri)
        {
            WriteEvent(16, uri);
        }

        [Event(eventId: 17, Level = EventLevel.Informational, Message = "Finished to delete blob {0}", Task = Tasks.DeleteBlob, Opcode = EventOpcode.Stop)]
        public void FinishedDelete(string uri)
        {
            WriteEvent(17, uri);
        }

        [Event(eventId: 18, Level = EventLevel.Error, Message = "Failed to delete blob {0}", Task = Tasks.DeleteBlob, Opcode = EventOpcode.Suspend)]
        public void FailedDelete(string uri)
        {
            WriteEvent(18, uri);
        }

        [Event(eventId: 19, Level = EventLevel.Informational, Message = "Beginning to renew lease for blob {0}", Task = Tasks.RenewLease, Opcode = EventOpcode.Start)]
        public void BeginningRenewLease(string uri)
        {
            WriteEvent(19, uri);
        }

        [Event(eventId: 20, Level = EventLevel.Informational, Message = "Finished to renew lease for blob {0}", Task = Tasks.RenewLease, Opcode = EventOpcode.Stop)]
        public void FinishedRenewLease(string uri)
        {
            WriteEvent(20, uri);
        }

        [Event(eventId: 21, Level = EventLevel.Error, Message = "Failed to renew lease for blob {0}", Task = Tasks.RenewLease, Opcode = EventOpcode.Suspend)]
        public void FailedRenewLease(string uri)
        {
            WriteEvent(21, uri);
        }

        [Event(eventId: 22, Level = EventLevel.Informational, Message = "Beginning to retrieve dimension '{0}'", Task = Tasks.RetrieveDimension, Opcode = EventOpcode.Start)]
        public void BeginningRetrieveDimension(string dimension)
        {
            WriteEvent(22, dimension);
        }

        [Event(eventId: 23, Level = EventLevel.Informational, Message = "Finished to retrieve dimension '{0}' ({1} ms)", Task = Tasks.RetrieveDimension, Opcode = EventOpcode.Stop)]
        public void FinishedRetrieveDimension(string dimension, long elapsedMilliseconds)
        {
            WriteEvent(23, dimension, elapsedMilliseconds);
        }

        [Event(eventId: 24, Level = EventLevel.Error, Message = "Failed to retrieve dimension '{0}'", Task = Tasks.RetrieveDimension, Opcode = EventOpcode.Suspend)]
        public void FailedRetrieveDimension(string dimension)
        {
            WriteEvent(24, dimension);
        }

        public static class Tasks
        {
            public const EventTask BlobListing = (EventTask)0x1;
            public const EventTask AcquireLease = (EventTask)0x2;
            public const EventTask DecompressBlob = (EventTask)0x3;
            public const EventTask ParseLog = (EventTask)0x4;
            public const EventTask UploadArchive = (EventTask)0x5;
            public const EventTask DeleteBlob = (EventTask)0x6;
            public const EventTask RenewLease = (EventTask)0x7;
            public const EventTask RetrieveDimension = (EventTask)0x8;
        }
    }
}