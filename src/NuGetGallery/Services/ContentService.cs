using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private static readonly Markdown MarkdownProcessor = new Markdown();

        private IDiagnosticsSource Trace { get; set; }

        public static readonly string ContentFileExtension = ".md";

        public IFileStorageService FileStorage { get; protected set; }

        protected ConcurrentDictionary<string, ContentItem> ContentCache { get { return _contentCache; } }

        protected ContentService() {
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

        public Task<HtmlString> GetContentItemAsync(string name, TimeSpan expiresIn)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, "name"), "name");
            }

            return GetContentItemCore(name, expiresIn);
        }

        // This NNNCore pattern allows arg checking to happen synchronously, before starting the async operation.
        private async Task<HtmlString> GetContentItemCore(string name, TimeSpan expiresIn)
        {
            using (Trace.Activity("GetContentItem " + name))
            {
                ContentItem item = null;
                if (ContentCache.TryGetValue(name, out item) && DateTime.UtcNow < item.ExpiryUtc)
                {
                    Trace.Information("Cache Valid. Expires at: " + item.ExpiryUtc.ToString());
                    return item.Content;
                }
                Trace.Information("Cache Expired.");

                // Get the file from the content service
                string fileName = name + ContentFileExtension;

                IFileReference reference;
                using (Trace.Activity("Downloading Content Item: " + fileName))
                {
                    reference = await FileStorage.GetFileReferenceAsync(
                        Constants.ContentFolderName,
                        fileName,
                        ifNoneMatch: item == null ? null : item.ContentId);
                }
                
                HtmlString result = new HtmlString(String.Empty);
                if (reference == null)
                {
                    Trace.Error("Requested Content File Not Found: " + fileName);
                }
                else
                {
                    // Check the content ID to see if it's different
                    if (item != null && String.Equals(item.ContentId, reference.ContentId, StringComparison.Ordinal))
                    {
                        Trace.Information("No change to content item. Using Cache");
                        // No change, just use the cache.
                        result = item.Content;
                    }
                    else
                    {
                        // Process the file
                        Trace.Information("Content Item changed. Updating...");
                        using (var stream = reference.OpenRead())
                        {
                            if (stream == null)
                            {
                                Trace.Error("Requested Content File Not Found: " + fileName);
                                reference = null;
                            }
                            else
                            {
                                using (Trace.Activity("Reading Content File: " + fileName))
                                using (var reader = new StreamReader(stream))
                                {
                                    result = new HtmlString(MarkdownProcessor.Transform(await reader.ReadToEndAsync()).Trim());
                                }
                            }
                        }
                    }
                }

                // Prep the new item for the cache
                item = new ContentItem(result, DateTime.UtcNow + expiresIn, reference == null ? null : reference.ContentId, DateTime.UtcNow);
                Trace.Information(String.Format("Updating Cache: {0} expires at {1}", name, item.ExpiryUtc));
                ContentCache.AddOrSet(name, item);

                // Return the result
                return result;
            }
        }

        public class ContentItem
        {
            public HtmlString Content { get; private set; }
            public string ContentId { get; private set; }
            public DateTime RetrievedUtc { get; private set; }
            public DateTime ExpiryUtc { get; private set; }

            public ContentItem(HtmlString content, DateTime expiryUtc, string contentId, DateTime retrievedUtc)
            {
                Content = content;
                ExpiryUtc = expiryUtc;
                ContentId = contentId;
                RetrievedUtc = retrievedUtc;
            }
        }
    }
}