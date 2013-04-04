using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGetGallery.TestUtils
{
    public class TestFile : IDisposable
    {
        private MemoryStream _buffer = new MemoryStream();

        public Stream Buffer { get { return _buffer; } }

        public string GetContentAsText()
        {
            return Encoding.Default.GetString(_buffer.ToArray());
        }

        public Stream Open()
        {
            _buffer.Seek(0, SeekOrigin.Begin);
            return _buffer;
        }

        public void Dispose()
        {
            _buffer.Dispose();
        }
    }
}
