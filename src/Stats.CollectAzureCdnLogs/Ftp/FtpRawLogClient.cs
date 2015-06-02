// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Stats.CollectAzureCdnLogs.Ftp
{
    internal sealed class FtpRawLogClient
        : IRawLogClient
    {
        private readonly JobEventSource _jobEventSource;
        private readonly string _username;
        private readonly string _password;

        public FtpRawLogClient(JobEventSource jobEventSource, string username, string password)
        {
            _jobEventSource = jobEventSource;
            _username = username;
            _password = password;
        }

        public JobEventSource EventSource
        {
            get { return _jobEventSource; }
        }

        public Task<Stream> OpenReadAsync(Uri uri)
        {
            return Task.FromResult((Stream)new FtpDownloadStream(this, uri));
        }

        public async Task<bool> RenameAsync(Uri uri, string newFileName)
        {
            Trace.TraceInformation("Renaming file '{0}' to '{1}'.", uri, newFileName);
            var request = CreateRequest(uri);
            request.Method = WebRequestMethods.Ftp.Rename;
            request.RenameTo = newFileName;

            var result = await TryGetResponseAsync(request, FtpStatusCode.FileActionOK);
            if (!result)
            {
                Trace.TraceWarning("Failed to rename file '{0}'.", uri);
            }
            return result;
        }

        public async Task DeleteAsync(Uri uri)
        {
            var uriString = uri.ToString();
            _jobEventSource.BeginningDelete(uriString);
            Trace.TraceInformation("Deleting file '{0}'.", uri);

            var request = CreateRequest(uri);
            request.Method = WebRequestMethods.Ftp.DeleteFile;

            var result = await TryGetResponseAsync(request, FtpStatusCode.FileActionOK);
            if (!result)
            {
                // A warning is OK here as the job should retry downloading and processing the file
                Trace.TraceWarning("Failed to delete file '{0}'.", uri);
            }
            else
            {
                _jobEventSource.FinishingDelete(uriString);
            }
        }

        public async Task<IEnumerable<RawLogFileInfo>> GetRawLogFiles(Uri uri)
        {
            var uriString = uri.ToString();
            _jobEventSource.BeginningDirectoryListing(uriString);
            Trace.TraceInformation("Listing directory '{0}'.", uri);

            var request = CreateRequest(uri);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            var webResponse = (FtpWebResponse)await request.GetResponseAsync();

            string directoryList;
            using (var responseStream = webResponse.GetResponseStream())
            {
                directoryList = await responseStream.GetStringAsync();
            }

            _jobEventSource.FinishingDirectoryListing(uriString);

            var fileNames = directoryList.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var rawLogFiles = fileNames.Select(fn => new RawLogFileInfo(new Uri(uri.EnsureTrailingSlash(), fn)));

            return rawLogFiles;
        }

        private static async Task<bool> TryGetResponseAsync(FtpWebRequest request, FtpStatusCode expectedResult)
        {
            for (int attempts = 0; attempts < 5; attempts++)
            {
                var response = (FtpWebResponse)await request.GetResponseAsync();
                if (response.StatusCode == expectedResult)
                {
                    return true;
                }
            }
            return false;
        }

        internal async Task<Stream> StartOrResumeFtpDownload(Uri uri, int contentOffset = 0)
        {
            if (contentOffset == 0)
            {
                _jobEventSource.BeginningDownload(uri.ToString());
                Trace.TraceInformation("Downloading file '{0}'.", uri);
            }
            else
            {
                _jobEventSource.ResumingDownload(uri.ToString(), contentOffset);
                Trace.TraceInformation("Resuming download of file '{0}' at content offset {1}.", uri, contentOffset);
            }

            var request = CreateRequest(uri);
            request.Method = WebRequestMethods.Ftp.DownloadFile;

            // allow for resume on failure
            request.ContentOffset = contentOffset;

            var webResponse = (FtpWebResponse)await request.GetResponseAsync();
            return webResponse.GetResponseStream();
        }

        private FtpWebRequest CreateRequest(Uri uri)
        {
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Credentials = new NetworkCredential(_username, _password);
            request.EnableSsl = true;

            return request;
        }

        public async Task<Uri> RenameAsync(RawLogFileInfo rawLogFile, string newFileName)
        {
            Uri rawLogUri;
            if (!rawLogFile.PendingDownload)
            {
                if (await RenameAsync(rawLogFile.Uri, newFileName))
                {
                    rawLogUri = new Uri(rawLogFile.Uri + FileExtensions.Download);
                }
                else
                {
                    // Failed (multiple times) to rename the file on the origin. No point in continuing with this file.
                    Trace.TraceError("Failed to rename file '{0}'. Processing aborted.", rawLogFile.Uri);
                    rawLogUri = null;
                }
            }
            else
            {
                rawLogUri = rawLogFile.Uri;
            }
            return rawLogUri;
        }
    }
}