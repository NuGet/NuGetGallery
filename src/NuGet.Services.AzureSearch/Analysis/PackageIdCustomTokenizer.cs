using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Splits tokens that on a set of symbols and whitespace.
    /// For example, "Foo.Bar Baz" becomes "Foo", "Bar", and "Baz".
    /// </summary>
    public static class PackageIdCustomTokenizer
    {
        public const string Name = "nuget_package_id_tokenizer";

        public static readonly PatternTokenizer Instance = new PatternTokenizer(
            Name,
            @"[.\-_,;:'*#!~+()\[\]{}\s]");
    }
}
