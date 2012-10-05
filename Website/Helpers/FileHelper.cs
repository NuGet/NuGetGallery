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

		public static bool IsBinaryFile(string path)
		{
			string extension = Path.GetExtension(path).ToUpper(CultureInfo.InvariantCulture);
			return String.IsNullOrEmpty(extension) || BinaryFileExtensions.Any(p => p.Equals(extension));
		}
	}
}