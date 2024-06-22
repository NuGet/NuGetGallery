// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Security.Policy;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.StaticAssets
{
    public class StaticAssetsTests : GalleryTestBase, IDisposable
    {
        public StaticAssetsTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            HttpClient = new HttpClient();
        }

        private static readonly Lazy<IReadOnlyList<string>> AssetPaths = new Lazy<IReadOnlyList<string>>(() => GetAssetPaths().ToList());
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Bundles = new Dictionary<string, IReadOnlyList<string>>
        {
            // CSS
            {
                "Content/css.min.css",
                new[]
                {
                    "Content/Site.css",
                    "Content/Layout.css",
                    "Content/PageStylings.css",
                    "Content/fabric.css",
                }
            },
            {
                "Content/gallery/css/site.min.css",
                new[]
                {
                    "Content/gallery/css/fabric.css",
                }
            },
            {
                "Content/themes/custom/page-support-requests.min.css",
                new[]
                {
                    "Content/themes/custom/jquery-ui-1.10.3.custom.css",
                    "Content/admin/SupportRequestStyles.css",
                }
            },

            // JavaScript
            {
                "Scripts/gallery/site.min.js",
                new[]
                {
                    "Scripts/gallery/jquery-3.4.1.js",
                    "Scripts/gallery/jquery.validate-1.16.0.js",
                    "Scripts/gallery/jquery.validate.unobtrusive-3.2.6.js",
                    "Scripts/gallery/knockout-3.4.2.js",
                    "Scripts/gallery/bootstrap.js",
                    "Scripts/gallery/moment-2.29.4.js",
                    "Scripts/gallery/common.js",
                    "Scripts/gallery/autocomplete.js",
                }
            },
            {
                "Scripts/gallery/stats.min.js",
                new[]
                {
                    "Scripts/d3/d3.js",
                    "Scripts/gallery/stats-perpackagestatsgraphs.js",
                    "Scripts/gallery/stats-dimensions.js",
                }
            },
            {
                "Scripts/gallery/page-display-package.min.js",
                new[]
                {
                    "Scripts/gallery/page-display-package.js",
                    "Scripts/gallery/clamp.js",
                }
            },
            {
                "Scripts/gallery/page-add-organization.min.js",
                new[]
                {
                    "Scripts/gallery/page-add-organization.js",
                    "Scripts/gallery/md5.js",
                }
            },
            {
                "Scripts/page-support-requests.min.js",
                new[]
                {
                    "Scripts/gallery/jquery-ui-1.10.3.js",
                    "Scripts/gallery/knockout-projections.js",
                    "Scripts/gallery/page-support-requests.js",
                }
            },
            {
                "Scripts/gallery/syntaxhighlight.min.js",
                new[]
                {
                    "Scripts/gallery/syntaxhighlight.js",
                }
            },
        };

        private static readonly HashSet<string> BundleInputPaths = new HashSet<string>(Bundles.SelectMany(x => x.Value));
        private static readonly IReadOnlyList<string> MinifiedFiles = new[] { "Content/gallery/css/bootstrap.min.css" };

        public static IEnumerable<object[]> AssetData => AssetPaths.Value.Select(x => new object[] { x });
        public static IEnumerable<object[]> BundleOutputData => Bundles.Select(x => new object[] { x.Key });
        public static IEnumerable<object[]> BundleInputExceptBundleOutputData => BundleInputPaths
            .Except(Bundles.Keys.Select(GetUnMinPath))
            .Select(x => new object[] { x });
        public static IEnumerable<object[]> MinifiedFilesData => MinifiedFiles.Select(x => new object[] { x });

        public HttpClient HttpClient { get; }

        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [MemberData(nameof(AssetData))]
        public async Task AllAssetsExistOnTheirOwn(string assetPath)
        {
            var bundleContent = await HttpClient.GetStringAsync(UrlHelper.BaseUrl + assetPath);

            Assert.DoesNotContain("Minification failed", Shorten(bundleContent), StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [MemberData(nameof(BundleOutputData))]
        public async Task NoBundleFailsMinification(string bundle)
        {
            var bundleContent = await HttpClient.GetStringAsync(UrlHelper.BaseUrl + bundle);

            Assert.DoesNotContain("Minification failed", Shorten(bundleContent), StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [MemberData(nameof(MinifiedFilesData))]
        public async Task NoFilesFailedMinification(string file)
        {
            var fileContent = await HttpClient.GetStringAsync(UrlHelper.BaseUrl + file);

            Assert.DoesNotContain("Minification failed", Shorten(fileContent), StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [MemberData(nameof(BundleInputExceptBundleOutputData))]
        public async Task BundledFilesDoNotExistAsMinified(string assetPath)
        {
            var minifiedAssetPath = GetMinPath(assetPath);

            using (var response = await HttpClient.GetAsync(UrlHelper.BaseUrl + minifiedAssetPath))
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }

        private static IEnumerable<string> GetAssetPaths()
        {
            var type = typeof(StaticAssetsTests);
            using (var stream = type.Assembly.GetManifestResourceStream(type.Namespace + ".Data.g.txt"))
            using (var reader = new StreamReader(stream))
            {
                // First line is the gallery directory itself.
                var firstLine = reader.ReadLine();
                if (firstLine == null)
                {
                    throw new InvalidOperationException("The generated list of static assets could not be read.");
                }

                var galleryDir = Path.GetFullPath(firstLine.TrimEnd('\\')) + '\\';
                string absolutePath;
                while ((absolutePath = reader.ReadLine()) != null)
                {
                    var fullPath = Path.GetFullPath(absolutePath);
                    if (!fullPath.StartsWith(galleryDir))
                    {
                        continue;
                    }

                    var relativePath = fullPath.Substring(galleryDir.Length);
                    yield return relativePath.Replace('\\', '/');
                }
            }
        }

        private static string GetMinPath(string assetPath)
        {
            var extension = Path.GetExtension(assetPath);
            var minifiedAssetPath = assetPath.Substring(0, assetPath.Length - extension.Length) + ".min" + extension;
            return minifiedAssetPath;
        }

        private static string GetUnMinPath(string assetPath)
        {
            return assetPath.Replace(".min", string.Empty);
        }

        /// <summary>
        /// If the assertion fails with a massive file, it gets ugly in Visual Studio. Like crashes. The minification
        /// error is at the top so we only need the beginning of the content.
        /// </summary>
        private static string Shorten(string content)
        {
            const int length = 1024;
            if (content.Length > length)
            {
                return content.Substring(0, 1024);
            }

            return content;
        }

        public void Dispose()
        {
            HttpClient.Dispose();
        }
    }
}