using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using MarkdownSharp;

namespace NuGetGallery.Services
{
    public class ContentService : IContentService
    {
        // Why not use the ASP.Net Cache?
        // Each entry should _always_ have a value. Values never expire they just need to be updated. Updates use the existing data,
        // so we don't want data just vanishing from a cache.
        private ConcurrentDictionary<string, ContentItem> _contentCache = new ConcurrentDictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);

        private static readonly Markdown MarkdownProcessor = new Markdown();
        
        public static readonly string ContentFolderName = "content";
        public static readonly string ContentFileExtension = ".md";

        public IFileStorageService FileStorage { get; protected set; }
        
        protected ConcurrentDictionary<string, ContentItem> ContentCache { get { return _contentCache; } }

        protected ContentService() { }
        public ContentService(IFileStorageService fileStorage)
        {
            if (fileStorage == null)
            {
                throw new ArgumentNullException("fileStorage");
            }
            
            FileStorage = fileStorage;
        }

        public Task<HtmlString> GetContentItemAsync(string name, TimeSpan expiresIn)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException(String.Format(Strings.ParameterCannotBeNullOrEmpty, "name"), "name");
            }

            return GetContentItemCore(name, expiresIn);
        }

        // This NNNCore pattern allows arg checking to happen synchronously, before starting the async operation.
        private async Task<HtmlString> GetContentItemCore(string name, TimeSpan expiresIn)
        {
            ContentItem item = null;
            if (ContentCache.TryGetValue(name, out item) && DateTime.UtcNow < item.ExpiryUtc)
            {
                return item.Content;
            }

            // Get the file from the content service
            string fileName = name + ContentFileExtension;
            var reference = await FileStorage.GetFileReferenceAsync(ContentFolderName, fileName, ifNoneMatch: item == null ? null : item.ContentId);

            // Process the file
            using (var reader = new StreamReader(reference.OpenRead()))
            {
                var result = new HtmlString(MarkdownProcessor.Transform(await reader.ReadToEndAsync()).Trim());

                // Store the content in the cache
                item = new ContentItem(result, expiresIn, reference.ContentId, DateTime.UtcNow);
                ContentCache.AddOrSet(name, item);

                // Return the result
                return result;
            }
        }

        public class ContentItem
        {
            public HtmlString Content { get; private set; }
            public TimeSpan ExpiresIn { get; private set; }
            public string ContentId { get; private set; }
            public DateTime RetrievedUtc { get; private set; }
            public DateTime ExpiryUtc { get { return RetrievedUtc + ExpiresIn; } }

            public ContentItem(HtmlString content, TimeSpan expiresIn, string contentId, DateTime retrievedUtc)
            {
                Content = content;
                ExpiresIn = expiresIn;
                ContentId = contentId;
                RetrievedUtc = retrievedUtc;
            }
        }
    }
}