using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NuGetGallery.AsyncFileUpload
{
    internal class AsyncFileUploadRequestParser
    {
        private int _totalLength;
        private byte[] _boundary;
        private Encoding _encoding;
        private List<byte> _contentDisposition = new List<byte>(200);

        private byte[] _newLine = new byte[] { 13, 10 };
        private int _newLineIndex;

        private ReadState _readState;
        private int _previousBoundaryStartIndex = -1;

        private int[] _kmpNext;
        private int _boundaryMatchIndex;

        private string _currentFileName;

        public string CurrentFileName
        {
            get
            {
                return _currentFileName;
            }
        }

        public AsyncFileUploadRequestParser(string boundary, Encoding encoding)
        {
            _encoding = encoding;
            _boundary = encoding.GetBytes(boundary);
            PrepareKmp(_boundary);
        }

        public void ParseNext(byte[] buffer, int count)
        {
            switch (_readState)
            {
                case ReadState.SearchForBoundary:
                    SearchForBoundary(buffer, 0, count);
                    break;

                case ReadState.ReadBeforeContentDisposition:
                    ReadNewLine(buffer, 0, count, true);
                    break;

                case ReadState.ReadAfterContentDisposition:
                    ReadNewLine(buffer, 0, count, false);
                    break;
            }

            _totalLength += count;
        }

        private void ReadNewLine(byte[] buffer, int startIndex, int lastIndex, bool beforeContentDisposition)
        {
            _readState = beforeContentDisposition ? ReadState.ReadBeforeContentDisposition : ReadState.ReadAfterContentDisposition;

            for (int i = startIndex; i < lastIndex; ++i)
            {
                if (!beforeContentDisposition)
                {
                    _contentDisposition.Add(buffer[i]);
                }

                if (buffer[i] == _newLine[_newLineIndex])
                {
                    _newLineIndex++;
                    if (_newLineIndex == 2)
                    {
                        _newLineIndex = 0;

                        if (beforeContentDisposition)
                        {
                            // found a new line character here. now start parsing the content-disposition 
                            _contentDisposition.Clear();
                            ReadNewLine(buffer, i + 1, lastIndex, false);
                        }
                        else
                        {
                            // reach the end of content-disposition. extract the filename out of it
                            _currentFileName = ParseContentDisposition(_contentDisposition.ToArray());
                            SearchForBoundary(buffer, i + 1, lastIndex);
                        }

                        return;
                    }
                }
                else
                {
                    _newLineIndex = 0;
                }
            }
        }

        private void SearchForBoundary(byte[] text, int startIndex, int lastIndex)
        {
            _readState = ReadState.SearchForBoundary;

            // continue where we left off from the previous buffer
            int i = _boundaryMatchIndex;
            int j = startIndex;
            while (j < lastIndex)
            {
                while (i > -1 && _boundary[i] != text[j])
                {
                    i = _kmpNext[i];
                }
                i++;
                j++;
                if (i >= _boundary.Length)
                {
                    // reset the boundary index
                    _boundaryMatchIndex = 0;

                    // found a match at (j-i)
                    int thisBoundaryStartIndex = j - i + _totalLength;
                    if (_previousBoundaryStartIndex > -1 && _currentFileName != null)
                    {
                        int previousFileSize = thisBoundaryStartIndex - _previousBoundaryStartIndex - _boundary.Length;
                        Debug.Assert(previousFileSize >= 0);
                    }
                    _previousBoundaryStartIndex = thisBoundaryStartIndex;

                    ReadNewLine(text, j, lastIndex, true);
                    return;
                }
            }
            // save the match index for the next buffer
            _boundaryMatchIndex = i;
        }

        private void PrepareKmp(byte[] pattern)
        {
            int len = pattern.Length;
            _kmpNext = new int[len];

            int pos = 2, cnd = 0;
            _kmpNext[0] = -1;
            _kmpNext[1] = 0;
            while (pos < len)
            {
                if (pattern[pos - 1] == pattern[cnd])
                {
                    _kmpNext[pos] = cnd + 1;
                    pos++;
                    cnd++;
                }
                else if (cnd > 0)
                {
                    cnd = _kmpNext[cnd];
                }
                else
                {
                    _kmpNext[pos] = 0;
                    pos++;
                }
            }
        }

        private string ParseContentDisposition(byte[] buffer)
        {
            // The Content-Disposition field follows immediately after the boundary. It has the format:
            //     Content-Disposition: form-data; name="asyncFileUpload"; filename="c:\users\documents\test.txt"
            // 
            // We want to extract the file name out of it.
            string content = _encoding.GetString(buffer);
            var match = Regex.Match(content, @"filename\=""(.*)\""");
            if (match.Success && match.Groups.Count > 1)
            {
                string filename = match.Groups[1].Value;
                return filename;
            }
            return null;
        }

        private enum ReadState
        {
            SearchForBoundary,
            ReadBeforeContentDisposition,
            ReadAfterContentDisposition
        }
    }
}