using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;

namespace NuGetGallery.Helpers
{
	internal static class FileHelper
	{
		private static readonly string[] BinaryFileExtensions = new[]
                                                                {
                                                                    ".DLL", ".EXE", ".WINMD", ".CHM", ".PDF",
                                                                    ".DOCX", ".DOC", ".JPG", ".PNG", ".GIF",
                                                                    ".RTF", ".PDB", ".ZIP", ".RAR", ".XAP",
                                                                    ".VSIX", ".NUPKG", ".SNK", ".PFX", ".ICO"
                                                                };

		private static readonly string[] ImageFileExtensions = new[]
                                                                {
                                                                    ".PNG", ".GIF", ".JPG"
                                                                };

		public static bool IsBinaryFile(string path)
		{
			string extension = Path.GetExtension(path).ToUpper(CultureInfo.InvariantCulture);
			return String.IsNullOrEmpty(extension) || BinaryFileExtensions.Any(p => p.Equals(extension));
		}

		public static bool IsImageFile(string path)
		{
			string extension = Path.GetExtension(path).ToUpper(CultureInfo.InvariantCulture);
			return String.IsNullOrEmpty(extension) || ImageFileExtensions.Any(p => p.Equals(extension));
		}

		internal static string GetMimeType(string filePath)
		{
			string extension = Path.GetExtension(filePath).ToLowerInvariant();
			if (extension.Equals(".png"))
			{
				return "image/png";
			}

			if (extension.Equals(".jpg"))
			{
				return "image/jpeg";
			}

			if (extension.Equals(".gif"))
			{
				return "image/gif";
			}

			return "image";
		}
	}
}