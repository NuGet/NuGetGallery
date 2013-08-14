using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGetGallery.Helpers
{
    public static class FileHelper
    {
        internal static readonly string[] BinaryFileExtensions = new[]
                                                                {
                                                                    ".DLL", ".EXE", ".WINMD", ".CHM", ".PDF",
                                                                    ".DOCX", ".DOC", ".RTF", ".PDB", ".ZIP", 
                                                                    ".RAR", ".XAP", ".VSIX", ".NUPKG", ".SNK", 
                                                                    ".PFX", ".PRI"
                                                                };

        internal static readonly string[] ImageFileExtensions = new[]
                                                                {
                                                                    ".PNG", ".GIF", ".JPG", ".BMP", ".JPEG", ".ICO"
                                                                };

        public static bool IsBinaryFile(string path)
        {
            string extension = Path.GetExtension(path);
            return String.IsNullOrEmpty(extension) || BinaryFileExtensions.Any(p => p.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return String.IsNullOrEmpty(extension) || ImageFileExtensions.Any(p => p.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetImageMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (ImageFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                if (extension == ".ico")
                {
                    // IE will only render .ico file if the mime type is "image/x-icon"
                    return "image/x-icon";
                }

                return "image/" + extension.Substring(1);	// omit the dot in front of extension
            }

            return "image";
        }

        public static string ReadTextTruncated(Stream stream, int maxLength)
        {
            const int BufferSize = 2 * 1024;
            var buffer = new char[BufferSize]; // read 2K at a time

            var sb = new StringBuilder();
            using (var reader = new StreamReader(stream))
            {
                while (sb.Length < maxLength)
                {
                    int bytesRead = reader.Read(buffer, 0, Math.Min(BufferSize, maxLength - sb.Length));
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(new string(buffer, 0, bytesRead));
                    }
                }

                // if not reaching the end of the stream yet, append the text "Truncating..."
                if (reader.Peek() > -1)
                {
                    // continue reading the rest of the current line to avoid dangling line
                    sb.AppendLine(reader.ReadLine());

                    if (reader.Peek() > -1)
                    {
                        sb.AppendLine().AppendLine("*** The rest of the content is truncated. ***");
                    }
                }
            }

            sb.Replace("\r\n", "\r");
            return sb.ToString();
        }
    }
}