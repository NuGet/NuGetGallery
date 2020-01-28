// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using ValueNormalizer = System.Collections.Generic.KeyValuePair<NuGet.Jobs.RegistrationComparer.ShouldNormalizeByPath, NuGet.Jobs.RegistrationComparer.NormalizeToken>;
using ArrayNormalizer = System.Collections.Generic.KeyValuePair<NuGet.Jobs.RegistrationComparer.ShouldNormalizeByArray, System.Comparison<Newtonsoft.Json.Linq.JToken>>;

namespace NuGet.Jobs.RegistrationComparer
{
    public delegate bool ShouldNormalizeByPath(string jsonPath);
    public delegate bool ShouldNormalizeByArray(JArray array);
    public delegate string NormalizeToken(JToken value, bool isLeft, ComparisonContext context);

    public class Normalizers
    {
        private readonly static HashSet<string> PackageIdsWithDotDotDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "angularjs.TypeScript.DefinitelyTyped",
            "devextreme.TypeScript.DefinitelyTyped",
        };

        private static readonly IReadOnlyList<ValueNormalizer> DefaultScalarNormalizers = new List<ValueNormalizer>
        {
            new ValueNormalizer(
                path => IsPropertyName(path, "@id") || IsPropertyName(path, "registration") || IsPropertyName(path, "parent"),
                (value, isLeft, context) =>
                {
                    var url = (string)value;
                    var baseUrl = isLeft ? context.LeftBaseUrl : context.RightBaseUrl;

                    // Ignore a quirky dependency package IDs:
                    //   - "../jquery.TypeScript.DefinitelyTyped"
                    //     https://api.nuget.org/v3/catalog0/data/2018.10.18.22.40.48/angularjs.typescript.definitelytyped.0.6.6.json
                    //   - "../jquery.TypeScript.DefinitelyTyped"
                    //     https://api.nuget.org/v3/catalog0/data/2018.12.15.06.50.14/devextreme.typescript.definitelytyped.0.0.4.json
                    if (PackageIdsWithDotDotDependencies.Contains(context.PackageId)
                        && IsPropertyName(value.Path, "registration")
                        && url.Contains("../"))
                    {
                        var otherBaseUrl = isLeft ? context.RightBaseUrl : context.LeftBaseUrl;
                        url = TrySetBaseUrl(url, baseUrl, otherBaseUrl);
                        url = new Uri(url).AbsoluteUri;
                    }

                    url = TrySetBaseUrl(url, baseUrl, "{base URL}/");

                    return url;
                }),
        };

        private static string TrySetBaseUrl(string url, string currentBaseUrl, string newBaseUrl)
        {
            if (url.StartsWith(currentBaseUrl))
            {
                url = newBaseUrl + url.Substring(currentBaseUrl.Length);
            }

            return url;
        }

        private static readonly IReadOnlyList<ValueNormalizer> IndexAndPageScalarNormalizers = new List<ValueNormalizer>(DefaultScalarNormalizers)
        {
            new ValueNormalizer(
                path => IsPropertyName(path, "commitId"),
                (value, isLeft, context) =>
                {
                    // Each iteration, the writer will come up with a different commit ID.
                    var commitId = (string)value;
                    if (commitId != null
                        && Regex.IsMatch(commitId, "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"))
                    {
                        return Guid.Empty.ToString();
                    }

                    return commitId;
                }),
            new ValueNormalizer(
                path => IsPropertyName(path, "commitTimeStamp"),
                (value, isLeft, context) =>
                {
                    // Each iteration, the writer will come up with a different commit timestamp.
                    var commitTimestamp = (string)value;
                    if (commitTimestamp != null
                        && Regex.IsMatch(commitTimestamp, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{1,}(Z|\+00:00)"))
                    {
                        return DateTimeOffset.MinValue.ToString("o");
                    }

                    return commitTimestamp;
                }), 
            new ValueNormalizer(
                path => IsPropertyName(path, "summary"),
                (value, isLeft, context) =>
                {
                    // The NuGet.Protocol.Catalog library deserializes properties in a case-insensitive way.
                    // This is due to the following Newtonsoft.Json issue:
                    // https://github.com/JamesNK/Newtonsoft.Json/issues/815

                    // This package has "Summary" instead of "summary"
                    // https://api.nuget.org/v3/catalog0/data/2019.10.02.07.14.49/staticproxy.fody.1.0.146-genericmethods.json
                    if (context.PackageId.Equals("StaticProxy.Fody", StringComparison.OrdinalIgnoreCase)
                        && (string)value.Parent.Parent["version"] == "1.0.146-GenericMethods")
                    {
                        return "";
                    }
                    
                    return (string)value;
                }),
            new ValueNormalizer(
                path => IsPropertyName(path, "licenseUrl"),
                (value, isLeft, context) =>
                {
                    // This package has "licenseurl" instead of "licenseUrl"
                    // https://api.nuget.org/v3/catalog0/data/2019.01.02.03.56.37/boolli.1.0.0.json
                    if (context.PackageId.Equals("Boolli", StringComparison.OrdinalIgnoreCase)
                        && (string)value.Parent.Parent["version"] == "1.0.0")
                    {
                        return "";
                    }

                    return (string)value;
                }),
            new ValueNormalizer(
                path => IsPropertyName(path, "projectUrl"),
                (value, isLeft, context) =>
                {
                    // This package has "ProjectUrl" instead of "projectUrl"
                    // https://api.nuget.org/v3/catalog0/data/2019.01.02.13.49.28/blogml.core.1.0.0.json
                    if (context.PackageId.Equals("BlogML.Core", StringComparison.OrdinalIgnoreCase)
                        && (string)value.Parent.Parent["version"] == "1.0.0")
                    {
                        return "";
                    }

                    return (string)value;
                }),
            new ValueNormalizer(
                path => IsPropertyName(path, "iconUrl"),
                (value, isLeft, context) =>
                {
                    // This package has "iconURL" instead of "iconUrl"
                    // https://api.nuget.org/v3/catalog0/data/2019.04.18.10.29.30/confuser.msbuild.2.0.0-alpha-0191.json
                    if (context.PackageId.Equals("Confuser.MSBuild", StringComparison.OrdinalIgnoreCase)
                        && (string)value.Parent.Parent["version"] == "2.0.0-alpha-0191")
                    {
                        return "";
                    }

                    return (string)value;
                }),
            new ValueNormalizer(
                path => IsPropertyName(path, "language"),
                (value, isLeft, context) =>
                {
                    // This package has "Language" instead of "language"
                    // https://api.nuget.org/v3/catalog0/data/2018.12.19.07.07.41/smartseeder.0.0.1.json
                    // https://api.nuget.org/v3/catalog0/data/2018.12.19.07.07.41/smartseeder.1.0.0-rc1-beta.json
                    // https://api.nuget.org/v3/catalog0/data/2018.12.19.07.07.31/smartseeder.1.0.0-rc1-preview.json
                    if (context.PackageId.Equals("SmartSeeder", StringComparison.OrdinalIgnoreCase)
                        && new[] { "0.0.1", "1.0.0-rc1-beta", "1.0.0-rc1-preview" }.Contains((string)value.Parent.Parent["version"]))
                    {
                        return "";
                    }

                    return (string)value;
                }),
            new ValueNormalizer(
                path => IsPropertyName(path, "range"),
                (value, isLeft, context) =>
                {
                    // This package has duplicate dependencies, meaning the dependency range is an array instead of string.
                    // https://api.nuget.org/v3/catalog0/data/2019.11.26.10.59.56/paket.core.5.237.1.json
                    // https://api.nuget.org/v3/catalog0/data/2019.11.26.11.13.24/paket.core.5.237.2.json
                    if (context.PackageId.Equals("Paket.Core", StringComparison.OrdinalIgnoreCase)
                        && new[] { "5.237.1", "5.237.2" }.Contains((string)value.Parent.Parent.Parent.Parent.Parent.Parent.Parent.Parent["version"])
                        && value.Type == JTokenType.Array)
                    {
                        return (string)value.OrderBy(x => x).First();
                    }

                    // This package has duplicate dependencies, meaning the dependency range is an array instead of string.
                    // https://api.nuget.org/v3/catalog0/data/2016.02.21.10.24.50/dingu.generic.repo.ef7.1.0.0.json
                    // https://api.nuget.org/v3/catalog0/data/2016.02.21.11.06.01/dingu.generic.repo.ef7.1.0.0-beta2.json
                    if (context.PackageId.Equals("Dingu.Generic.Repo.EF7", StringComparison.OrdinalIgnoreCase)
                        && new[] { "1.0.0", "1.0.0-beta2" }.Contains((string)value.Parent.Parent.Parent.Parent.Parent.Parent.Parent.Parent["version"])
                        && value.Type == JTokenType.Array)
                    {
                        return (string)value.OrderBy(x => x).First();
                    }

                    return (string)value;
                }),
        };

        private static readonly IReadOnlyList<ShouldNormalizeByPath> DefaultUnsortedObjects = new List<ShouldNormalizeByPath>
        {
            path => path.StartsWith("@context."),
        };

        private static readonly IReadOnlyList<ShouldNormalizeByPath> PageUnsortedObjects = new List<ShouldNormalizeByPath>(DefaultUnsortedObjects)
        {
            path => path == string.Empty,
        };

        private static readonly IReadOnlyList<ArrayNormalizer> DefaultUnsortedArrays = new List<ArrayNormalizer>();

        private static readonly IReadOnlyList<ArrayNormalizer> IndexAndPageUnsortedArrays = new List<ArrayNormalizer>(DefaultUnsortedArrays)
        {
            new ArrayNormalizer(
                a => IsPropertyName(a.Path, "dependencies"),
                (a, b) => StringComparer.OrdinalIgnoreCase.Compare((string)a["id"], (string)b["id"])),
            new ArrayNormalizer(
                a => IsPropertyName(a.Path, "tags"),
                (a, b) => StringComparer.Ordinal.Compare((string)a, (string)b)),
            new ArrayNormalizer(
                a => IsPropertyName(a.Path, "reasons"),
                (a, b) => StringComparer.Ordinal.Compare((string)a, (string)b)),
            new ArrayNormalizer(
                a => IsPropertyName(a.Path, "dependencyGroups"),
                (a, b) => StringComparer.Ordinal.Compare((string)a["targetFramework"], (string)b["targetFramework"])),
            new ArrayNormalizer(
                a => IsPropertyName(a.Path, "items")
                  && a.Parent?.Parent != null
                  && a.Parent.Parent["@type"]?.Type == JTokenType.String
                  && (string)a.Parent.Parent["@type"] == "catalog:CatalogPage",
                (a, b) => NuGetVersion.Parse((string)a["catalogEntry"]["version"]).CompareTo(NuGetVersion.Parse((string)b["catalogEntry"]["version"])))
        };

        private static bool IsPropertyName(string path, string name)
        {
            return path == name || path.EndsWith("." + name);
        }

        public Normalizers(
            IReadOnlyList<ValueNormalizer> scalarNormalizers,
            IReadOnlyList<ShouldNormalizeByPath> unsortedObjects,
            IReadOnlyList<ArrayNormalizer> unsortedArrays)
        {
            ScalarNormalizers = scalarNormalizers;
            UnsortedObjects = unsortedObjects;
            UnsortedArrays = unsortedArrays;
        }

        public static readonly Normalizers Index = new Normalizers(
            IndexAndPageScalarNormalizers,
            DefaultUnsortedObjects,
            IndexAndPageUnsortedArrays);

        public static readonly Normalizers Page = new Normalizers(
            IndexAndPageScalarNormalizers,
            PageUnsortedObjects,
            IndexAndPageUnsortedArrays);

        public static readonly Normalizers Leaf = new Normalizers(
            DefaultScalarNormalizers,
            DefaultUnsortedObjects,
            DefaultUnsortedArrays);

        public IReadOnlyList<ValueNormalizer> ScalarNormalizers { get; }
        public IReadOnlyList<ShouldNormalizeByPath> UnsortedObjects { get; }
        public IReadOnlyList<ArrayNormalizer> UnsortedArrays { get; }
    }
}
