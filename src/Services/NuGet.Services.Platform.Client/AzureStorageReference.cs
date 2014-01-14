using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Services
{
    public class AzureStorageReference
    {
        public static readonly string AzureStorageScheme = "azstore";

        public string AccountName { get; private set; }
        public string AccountKey { get; private set; }
        public string Container { get; private set; }
        public string Path { get; private set; }

        public AzureStorageReference(string accountName, string container, string path)
            : this(accountName, String.Empty, container, path) { }
        public AzureStorageReference(string accountName, string accountKey, string container, string path)
        {
            AccountName = accountName;
            AccountKey = accountKey;
            Container = container;
            Path = path;
        }

        public static bool TryCreate(Uri sourceUri, out AzureStorageReference parsed)
        {
            return TryCreateCore(sourceUri, throwOnError: false, parsed: out parsed);
        }

        public static AzureStorageReference Create(Uri sourceUri)
        {
            AzureStorageReference parsed;
            TryCreateCore(sourceUri, throwOnError: true, parsed: out parsed);
            return parsed;
        }

        private static readonly Regex PathParser = new Regex("^/(?<container>[^/]*)(/(?<path>.*))?$");
        private static bool TryCreateCore(Uri sourceUri, bool throwOnError, out AzureStorageReference parsed)
        {
            parsed = null;
            if (!sourceUri.IsAbsoluteUri)
            {
                if (throwOnError)
                {
                    throw new FormatException(Strings.AzureStorageAccount_RequiresAbsoluteUri);
                }
                return false;
            }

            if (!String.Equals(sourceUri.Scheme, AzureStorageScheme))
            {
                if (throwOnError)
                {
                    throw new FormatException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.AzureStorageAccount_InvalidScheme,
                        sourceUri.Scheme,
                        AzureStorageScheme));
                }
                return false;
            }

            var fullPath = sourceUri.AbsolutePath;
            var match = PathParser.Match(fullPath);
            if (!match.Success)
            {
                if (throwOnError)
                {
                    throw new FormatException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.AzureStorageAccount_RequiresContainer,
                        AzureStorageScheme));
                }
                return false;
            }
            parsed = new AzureStorageReference(
                sourceUri.Host,
                WebUtility.UrlDecode(sourceUri.UserInfo),
                match.Groups["container"].Value,
                match.Groups["path"].Value);
            return true;
        }
    }
}
