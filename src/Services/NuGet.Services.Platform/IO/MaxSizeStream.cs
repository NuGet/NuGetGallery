using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.IO
{
    public class MaxSizeStream : Stream
    {
        public long MaximumAllowedLength { get; private set; }
        public Stream InnerStream { get; private set; }
        public bool LeaveOpen { get; private set; }
        protected long RemainingLength { get { return MaximumAllowedLength - Position; } }
        
        public MaxSizeStream(Stream stream, int maxAllowedLength) : this(stream, maxAllowedLength, leaveOpen: false) { }
        public MaxSizeStream(Stream stream, int maxAllowedLength, bool leaveOpen)
        {
            InnerStream = stream;
            MaximumAllowedLength = maxAllowedLength;
            LeaveOpen = leaveOpen;
        }

        public override bool CanRead { get { return InnerStream.CanRead; } }
        public override bool CanSeek { get { return InnerStream.CanSeek; } }
        public override bool CanWrite { get { return InnerStream.CanWrite; } }
        public override bool CanTimeout { get { return InnerStream.CanTimeout; } }

        public override void Flush()
        {
            InnerStream.Flush();
        }

        public override long Length
        {
            get { return Math.Min(MaximumAllowedLength, InnerStream.Length); }
        }

        public override long Position
        {
            get
            {
                return InnerStream.Position;
            }
            set
            {
                if (value >= MaximumAllowedLength)
                {
                    throw CreateOutOfRangeException();
                }
                InnerStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long allowed = Math.Min(RemainingLength, count);
            if(allowed == 0) {
                return 0;
            }
            return InnerStream.Read(buffer, offset, allowed);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    if (offset >= MaximumAllowedLength)
                    {
                        throw CreateOutOfRangeException();
                    }
                    break;
                case SeekOrigin.Current:
                    long frontOffset = offset + Position;
                    if (frontOffset >= MaximumAllowedLength)
                    {
                        throw CreateOutOfRangeException();
                    }
                    break;
                default:
                    long backOffset = Length - offset;
                    if (backOffset >= MaximumAllowedLength)
                    {
                        throw CreateOutOfRangeException();
                    }
                    break;
            }
            return InnerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            if (value > MaximumAllowedLength)
            {
                throw CreateOutOfRangeException();
            }
            InnerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (RemainingLength < count)
            {
                throw CreateOutOfRangeException();
            }
            InnerStream.Write(buffer, offset, allowed);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            long allowed = Math.Min(RemainingLength, count);
            if (allowed == 0)
            {
                var ar = new SynchronousAsyncResult<int>(0, state);
                if (callback != null)
                {
                    callback(ar);
                }
                return ar;
            }
            return base.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (RemainingLength < count)
            {
                throw CreateOutOfRangeException();
            }
            return base.BeginWrite(buffer, offset, count, callback, state);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            long allowed = Math.Min(RemainingLength, count);
            if (allowed == 0)
            {
                return Task.FromResult(0);
            }
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (RemainingLength < count)
            {
                throw CreateOutOfRangeException();
            }
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return InnerStream.FlushAsync();
        }

        public override void WriteByte(byte value)
        {
            if (RemainingLength < 1)
            {
                throw CreateOutOfRangeException();
            }
            InnerStream.WriteByte(value);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            SynchronousAsyncResult<int> syncResult = asyncResult as SynchronousAsyncResult<int>;
            if (syncResult != null)
            {
                return syncResult.Value;
            }
            return base.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            return InnerStream.EndWrite(asyncResult);
        }

        public override int ReadByte()
        {
            if (RemainingLength < 1)
            {
                throw CreateOutOfRangeException(); 
            }
            return InnerStream.ReadByte();
        }

        public override int ReadTimeout
        {
            get
            {
                return InnerStream.ReadTimeout;
            }
            set
            {
                InnerStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return InnerStream.WriteTimeout;
            }
            set
            {
                InnerStream.WriteTimeout = value;
            }
        }

        private ArgumentOutOfRangeException CreateOutOfRangeException()
        {
            return new ArgumentOutOfRangeException(String.Format(
                                        CultureInfo.CurrentCulture,
                                        Strings.MaxSizeStream_PositionIsPastMaximumAllowedLength,
                                        MaximumAllowedLength));
        }

        private class SynchronousAsyncResult<T> : IAsyncResult
        {
            private ManualResetEventSlim _event = new ManualResetEventSlim(initialState: true);

            public T Value { get; private set; }

            public object AsyncState { get; private set; }
            public WaitHandle AsyncWaitHandle { get { return _event.WaitHandle; } }

            public bool CompletedSynchronously { get { return true; } }
            public bool IsCompleted { get { return true; } }

            public SynchronousAsyncResult(T value, object asyncState)
            {
                Value = value;
                AsyncState = asyncState;
            }
        }

    }
}
