// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Stats.CollectAzureCdnLogs
{
    [EventSource(Name = "NetFoundation-NuGet-Jobs-CollectAzureCdnLogs")]
    public class JobEventSource
        : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();

        private JobEventSource()
        {
        }

        [Event(eventId: 1, Level = EventLevel.Informational, Message = "Beginning directory listing from {0}", Task = Tasks.Downloading, Opcode = EventOpcode.Start)]
        public void BeginningDirectoryListing(string uri)
        {
            WriteEvent(1, uri);
        }

        [Event(eventId: 2, Level = EventLevel.Informational, Message = "Finishing directory listing from {0}", Task = Tasks.Downloading, Opcode = EventOpcode.Stop)]
        public void FinishingDirectoryListing(string uri)
        {
            WriteEvent(2, uri);
        }

        [Event(eventId: 3, Level = EventLevel.Informational, Message = "Beginning download from {0}", Task = Tasks.Downloading, Opcode = EventOpcode.Start)]
        public void BeginningDownload(string uri)
        {
            WriteEvent(3, uri);
        }

        [Event(eventId: 4, Level = EventLevel.Informational, Message = "Resuming download from {0} at content offset {1}", Task = Tasks.Downloading, Opcode = EventOpcode.Resume)]
        public void ResumingDownload(string uri, int contentOffset)
        {
            WriteEvent(4, uri, contentOffset);
        }

        [Event(eventId: 5, Level = EventLevel.Informational, Message = "Finishing download from {0}", Task = Tasks.Downloading, Opcode = EventOpcode.Stop)]
        public void FinishedDownload(string uri)
        {
            WriteEvent(5, uri);
        }

        [Event(eventId: 6, Level = EventLevel.Informational, Message = "Beginning blob upload: {0}", Task = Tasks.Uploading, Opcode = EventOpcode.Start)]
        public void BeginningBlobUpload(string blobName)
        {
            WriteEvent(6, blobName);
        }

        [Event(eventId: 7, Level = EventLevel.Informational, Message = "Finishing blob upload: {0}", Task = Tasks.Uploading, Opcode = EventOpcode.Stop)]
        public void FinishingBlobUpload(string blobName)
        {
            WriteEvent(7, blobName);
        }

        [Event(eventId: 8, Level = EventLevel.Informational, Message = "Beginning delete: {0}", Task = Tasks.Deleting, Opcode = EventOpcode.Start)]
        public void BeginningDelete(string uri)
        {
            WriteEvent(8, uri);
        }

        [Event(eventId: 9, Level = EventLevel.Informational, Message = "Finishing delete: {0}", Task = Tasks.Deleting, Opcode = EventOpcode.Stop)]
        public void FinishingDelete(string uri)
        {
            WriteEvent(9, uri);
        }

        [Event(eventId: 10, Level = EventLevel.Error, Message = "Failed to rename file {0} to {1}", Task = Tasks.Renaming, Opcode = EventOpcode.Suspend)]
        public void FailedToRenameFile(string uri, string newFileName)
        {
            WriteEvent(10, uri, newFileName);
        }

        [Event(eventId: 11, Level = EventLevel.Error, Message = "Failed to delete file {0}", Task = Tasks.Deleting, Opcode = EventOpcode.Suspend)]
        public void FailedToDeleteFile(string uri)
        {
            WriteEvent(11, uri);
        }

        [Event(eventId: 12, Level = EventLevel.Error, Message = "Failed to download file {0}: {1}", Task = Tasks.Downloading, Opcode = EventOpcode.Suspend)]
        public void FailedToDownloadFile(string uri, string exception)
        {
            WriteEvent(12, uri, exception);
        }

        [Event(eventId: 13, Level = EventLevel.Error, Message = "Failed to download file {0}: {1}", Task = Tasks.ListingDirectory, Opcode = EventOpcode.Suspend)]
        public void FailedToGetRawLogFiles(string uri, string exception)
        {
            WriteEvent(13, uri, exception);
        }

        [Event(eventId: 14, Level = EventLevel.Error, Message = "Failed to upload file {0}: {1}", Task = Tasks.Uploading, Opcode = EventOpcode.Suspend)]
        public void FailedToUploadFile(string uri, string exception)
        {
            WriteEvent(14, uri, exception);
        }

        public static class Tasks
        {
            public const EventTask Downloading = (EventTask)0x1;
            public const EventTask Uploading = (EventTask)0x2;
            public const EventTask Deleting = (EventTask)0x3;
            public const EventTask ListingDirectory = (EventTask)0x4;
            public const EventTask Renaming = (EventTask)0x5;
        }
    }
}