using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Splits tokens that on a set of symbols.
    /// For example, "Foo.Bar" becomes "Foo" and "Bar".
    /// </summary>
    public static class PackageIdCustomTokenizer
    {
        public const string Name = "nuget_package_id_tokenizer";

        public static readonly PatternTokenizer Instance = new PatternTokenizer(
            Name,
            @"[.\-_,;:'*#!~+()\[\]{}]");
    }
}
