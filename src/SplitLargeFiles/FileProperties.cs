// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;

namespace NuGet.Tools.SplitLargeFiles
{
    public class FileProperties
    {
        private const int BufferSize = 16 * 1024;

        public FileProperties(string path, long size, bool isGzipped, Encoding encoding, int lineCount)
        {
            Path = path;
            Size = size;
            IsGzipped = isGzipped;
            Encoding = encoding;
            LineCount = lineCount;
        }

        public string Path { get; }
        public long Size { get; }
        public bool IsGzipped { get; }
        public Encoding Encoding { get; }
        public int LineCount { get; }

        public static FileProperties FromFileStream(FileStream stream)
        {
            var path = stream.Name;
            var size = stream.Length;
            var isGzipped = DetectGzip(stream);
            var encoding = DetectEncoding(isGzipped, stream);
            var lineCount = CountLines(isGzipped, encoding, stream);

            return new FileProperties(
                path,
                size,
                isGzipped,
                encoding,
                lineCount);
        }

        private static byte[] GetLeadingBytes(Stream stream, int count)
        {
            var initialPosition = stream.Position;
            stream.Position = 0;
            var buffer = new byte[count];
            var read = stream.Read(buffer, 0, buffer.Length);
            var bytes = new byte[read];
            Buffer.BlockCopy(buffer, 0, bytes, 0, read);
            stream.Position = initialPosition;
            return bytes;
        }

        private static bool DetectGzip(Stream stream)
        {
            var header = GetLeadingBytes(stream, 2);
            if (header.Length < 2 || header[0] != 0x1f || header[1] != 0x8b)
            {
                return false;
            }

            var initialPosition = stream.Position;
            stream.Position = 0;
            try
            {
                using (var gzipStream = new GZipInputStream(stream) { IsStreamOwner = false })
                {
                    var buffer = new byte[128];
                    gzipStream.Read(buffer, 0, buffer.Length);
                }
            }
            catch (InvalidDataException)
            {
                return false;
            }
            stream.Position = initialPosition;

            return true;
        }

        private static Encoding DetectEncoding(bool isGzipped, Stream stream)
        {
            return ProcesStream(
                isGzipped,
                stream,
                readStream =>
                {
                    using (var reader = new StreamReader(
                        readStream,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: true,
                        bufferSize: BufferSize,
                        leaveOpen: true))
                    {
                        reader.Peek();
                        return reader.CurrentEncoding;
                    }
                });
        }

        private static int CountLines(bool isGzipped, Encoding encoding, Stream stream)
        {
            return ProcesStream(
                isGzipped,
                stream,
                readStream =>
                {
                    Console.Write("Counting lines...");
                    var lineCount = 0;
                    using (var reader = new StreamReader(
                        readStream,
                        encoding,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: BufferSize,
                        leaveOpen: true))
                    {
                        string line;
                        var stopwatch = Stopwatch.StartNew();
                        while ((line = reader.ReadLine()) != null)
                        {
                            lineCount++;
                            if (lineCount % 1_000_000 == 0)
                            {
                                Console.WriteLine();
                                var percent = 1.0 * reader.BaseStream.Position / stream.Length;
                                var totalTimeEst = stopwatch.ElapsedTicks / percent;
                                var remainingTicks = (long)((1 - percent) * totalTimeEst);
                                var remainingMins = TimeSpan.FromTicks(remainingTicks).TotalMinutes;
                                Console.Write($"[{percent:P} - ETA {remainingMins:F2}m] {lineCount} lines");
                            }
                            else if (lineCount % 25_000 == 0)
                            {
                                Console.Write(".");
                            }
                        }
                    }

                    Console.WriteLine();
                    return lineCount;
                });
            
        }

        private static T ProcesStream<T>(bool isGzipped, Stream stream, Func<Stream, T> processAsync)
        {
            var initialPosition = stream.Position;
            stream.Position = 0;

            var readStream = stream;
            if (isGzipped)
            {
                readStream = new GZipInputStream(stream) { IsStreamOwner = false };
            }

            var output = default(T);
            try
            {
                output = processAsync(readStream);
            }
            finally
            {
                if (isGzipped)
                {
                    readStream.Dispose();
                }
            }

            stream.Position = initialPosition;
            return output;
        }
    }
}
