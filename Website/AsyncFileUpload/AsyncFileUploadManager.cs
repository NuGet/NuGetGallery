using System.Collections.Concurrent;

namespace NuGetGallery.AsyncFileUpload
{
    public static class AsyncFileUploadManager
    {
        private static readonly ConcurrentDictionary<string, AsyncFileUploadProgress> _Progress =
            new ConcurrentDictionary<string, AsyncFileUploadProgress>();

        public static void SetProgressDetails(string key, AsyncFileUploadProgress progressDetails)
        {
            _Progress[key] = progressDetails;
        }

        public static AsyncFileUploadProgress GetProgressDetails(string key)
        {
            AsyncFileUploadProgress progressDetails;
            if (!_Progress.TryGetValue(key, out progressDetails))
            {
                progressDetails = null;
            }

            return progressDetails;
        }

        public static void RemoveProgressDetails(string key)
        {
            AsyncFileUploadProgress details;
            _Progress.TryRemove(key, out details);
        }
    }
}