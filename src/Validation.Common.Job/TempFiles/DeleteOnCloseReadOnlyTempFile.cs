// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// Opens existing file for exclusive read and deletes it on close.
    /// </summary>
    public class DeleteOnCloseReadOnlyTempFile : ITempReadOnlyFile
    {
        private const int BufferSize = 8192;
        private readonly FileStream _fileStream;

        public DeleteOnCloseReadOnlyTempFile(string fileName)
        {
            _fileStream = FileStreamUtility.OpenTemporaryFile(fileName);
        }

        public async Task<string> ReadToEndAsync()
        {
            using (var streamReader = new StreamReader(ReadStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: BufferSize, leaveOpen: true))
            {
                return await streamReader.ReadToEndAsync();
            }
        }

        public Stream ReadStream => _fileStream;

        public string FullName => _fileStream.Name;

        public void Dispose()
        {
            _fileStream.Dispose();
        }
    }
}
