using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using MarkdownSharp;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public class ContentService : IContentService
    {
        // Why not use the ASP.Net Cache?
        // Each entry should _always_ have a value. Values never expire, they just need to be updated. Updates use the existing data,
        // so we don't want data just vanishing from a cache.
        private ConcurrentDictionary<string, ContentItem> _contentCache = new ConcurrentDictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);

        private IDiagnosticsSource Trace { get; set; }

        public static readonly string HtmlContentFileExtension = ".html";
        public static readonly string MarkdownContentFileExtension = ".md";

        public static readonly string JsonContentFileExtension = ".json";

        public IFileStorageService FileStorage { get; protected set; }

        protected ConcurrentDictionary<string, ContentItem> ContentCache { get { return _contentCache; } }

        protected ContentService()
        {
            Trace = NullDiagnosticsSource.Instance;
        }

        public ContentService(IFileStorageService fileStorage, IDiagnosticsService diagnosticsService)
        {
            if (fileStorage == null)
            {
                throw new ArgumentNullException("fileStorage");
            }

            if (diagnosticsService == null)
            {
                throw new ArgumentNullException("diagnosticsService");
            }

            FileStorage = fileStorage;
            Trace = diagnosticsService.GetSource("ContentService");
        }
        public void ClearCache()
        {
            _contentCache.Clear();
        }

        public Task<IHtmlString> GetContentItemAsync(string name, TimeSpan expiresIn)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, "name"), "name");
            }

            return GetContentItemCore(name, expiresIn);
        }

        // This NNNCore pattern allows arg checking to happen synchronously, before starting the async operation.
        private async Task<IHtmlString> GetContentItemCore(string name, TimeSpan expiresIn)
        {
            using (Trace.Activity("GetContentItem " + name))
            {
                ContentItem cachedItem = null;
                if (ContentCache.TryGetValue(name, out cachedItem) && DateTime.UtcNow < cachedItem.ExpiryUtc)
                {
                    Trace.Verbose("Cache Valid. Expires at: " + cachedItem.ExpiryUtc.ToString());
                    return cachedItem.Content;
                }
                Trace.Verbose("Cache Expired.");

                // Get the file from the content service
                var filenames = new[] {
                    name + HtmlContentFileExtension,
                    name + MarkdownContentFileExtension,
                    name + JsonContentFileExtension
                };

                foreach (var filename in filenames)
                {
                    ContentItem item = await RefreshContentFromFile(filename, cachedItem, expiresIn);
                    if (item != null)
                    {
                        // Cache and return the result
                        Debug.Assert(item.Content != null);
                        ContentCache.AddOrSet(name, item);
                        return item.Content;
                    }
                }

                return new HtmlString(String.Empty);
            }
        }

        private async Task<ContentItem> RefreshContentFromFile(string fileName, ContentItem cachedItem, TimeSpan expiresIn)
        {
            using (Trace.Activity("Downloading Content Item: " + fileName))
            {
                IFileReference reference = await FileStorage.GetFileReferenceAsync(
                    Constants.ContentFolderName,
                    fileName,
                    ifNoneMatch: cachedItem == null ? null : cachedItem.ContentId);

                if (reference == null)
                {
                    Trace.Error("Requested Content File Not Found: " + fileName);
                    return null;
                }

                // Check the content ID to see if it's different
                if (cachedItem != null && String.Equals(cachedItem.ContentId, reference.ContentId, StringComparison.Ordinal))
                {
                    Trace.Verbose("No change to content item. Using Cache");

                    // Update the expiry time
                    cachedItem.ExpiryUtc = DateTime.UtcNow + expiresIn;
                    Trace.Verbose(String.Format("Updating Cache: {0} expires at {1}", fileName, cachedItem.ExpiryUtc));
                    return cachedItem;
                }

                // Retrieve the content
                Trace.Verbose("Content Item changed. Trying to update...");
                try
                {
                    using (var stream = reference.OpenRead())
                    {
                        if (stream == null)
                        {
                            Trace.Error("Requested Content File Not Found: " + fileName);
                            return null;
                        }
                        else
                        {
                            using (Trace.Activity("Reading Content File: " + fileName))
                            {
                                using (var reader = new StreamReader(stream))
                                {
                                    string text = await reader.ReadToEndAsync();
                                    string content;

                                    if (fileName.EndsWith(".md"))
                                    {
                                        content = new Markdown().Transform(text);
                                    }
                                    else
                                    {
                                        content = text;
                                    }

                                    IHtmlString html = new HtmlString(content.Trim());

                                    // Prep the new item for the cache
                                    var expiryTime = DateTime.UtcNow + expiresIn;
                                    return new ContentItem(html, expiryTime, reference.ContentId, DateTime.UtcNow);
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.Assert(false, "owchy oochy - reading content failed");
                    Trace.Error("Reading updated content failed. Returning cached content instead.");
                    return cachedItem;
                }
            }
        }

        public class ContentItem
        {
            public IHtmlString Content { get; private set; }
            public string ContentId { get; private set; }
            public DateTime RetrievedUtc { get; private set; }
            public DateTime ExpiryUtc { get; set; }

            public ContentItem(IHtmlString content, DateTime expiryUtc, string contentId, DateTime retrievedUtc)
            {
                Content = content;
                ExpiryUtc = expiryUtc;
                ContentId = contentId;
                RetrievedUtc = retrievedUtc;
            }
        }
    }
}