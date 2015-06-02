// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stats.CollectAzureCdnLogs.Ftp
{
    internal sealed class FtpDownloadStream
        : Stream
    {
        private readonly FtpRawLogClient _client;
        private readonly Uri _uri;
        private Stream _stream;
        private int _totalDone;
        private bool _disposing;

        public Exception CaughtException { get; private set; }

        public FtpDownloadStream(FtpRawLogClient client, Uri uri)
        {
            _client = client;
            _uri = uri;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int attempts = 0;
            while (attempts++ < 5)
            {
                if (_stream == null)
                {
                    _stream = await _client.StartOrResumeFtpDownload(_uri, _totalDone);

                    _client.EventSource.FinishedDownload(_uri.ToString());
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

                    // Close ftp resources if possible. Set instances to null to force restart.
                    Close();
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

                    _client.EventSource.FinishedDownload(_uri.ToString());
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

                    // Close ftp resources if possible. Set instances to null to force restart.
                    Close();
                }
            }
            return 0;
        }

        public override void Close()
        {
            if (_disposing)
            {
                return;
            }

            _disposing = true;

            if (_stream != null)
            {
                try
                {
                    _stream.Close();
                }
                catch
                {
                    // No action required
                }
            }
            _stream = null;
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