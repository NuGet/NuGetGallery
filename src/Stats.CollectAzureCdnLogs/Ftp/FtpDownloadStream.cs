// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stats.CollectAzureCdnLogs.Ftp
{
    internal sealed class FtpDownloadStream
        : Stream
    {
        private readonly FtpRawLogClient _client;
        private readonly Uri _uri;
        private Stream _stream;
        private int _totalDone;

        public Exception CaughtException { get; private set; }

        public FtpDownloadStream(FtpRawLogClient client, Uri uri)
        {
            _client = client;
            _uri = uri;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var attempts = 0;
            while (attempts++ < 5)
            {
                if (_stream == null)
                {
                    _stream = await _client.StartOrResumeFtpDownload(_uri, _totalDone);

                    _client.Logger.LogInformation("Finishing download from '{FtpBlobUri}'", _uri);
                }
                try
                {
                    // This will throw a timeout exception if the connection is interrupted.
                    // Will throw null exception if failed to open (start); this will also retry.
                    int done = await _stream.ReadAsync(buffer, offset, count, cancellationToken);

                    _totalDone += done;
                    return done;
                }
                catch (Exception ex)
                {
                    CaughtException = ex;

                    _client.Logger.LogError(0, ex, "Failed to download file after {Attempts} attempts", attempts);

                    // Close ftp resources and set instance to null to restart the download.
                    _stream?.Dispose();
                    _stream = null;
                }
            }
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int attempts = 0;
            while (attempts++ < 5)
            {
                // Adjust the maximum attempts according to your needs
                if (_stream == null)
                {
                    _stream = _client.StartOrResumeFtpDownload(_uri, _totalDone).Result;

                    _client.Logger.LogInformation("Finishing download from '{FtpBlobUri}'", _uri);
                }
                try
                {
                    // This will throw a timeout exception if the connection is interrupted.
                    // Will throw null exception if failed to open (start); this will also retry.
                    int done = _stream.ReadAsync(buffer, offset, count).Result;

                    _totalDone += done;
                    return done;
                }
                catch (Exception ex)
                {
                    CaughtException = ex;

                    _client.Logger.LogError(0, ex, "Failed to download file after {Attempts}", attempts);

                    // Close ftp resources and set instance to null to restart the download.
                    _stream?.Dispose();
                    _stream = null;
                }
            }
            return 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream?.Dispose();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }
        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
    }
}