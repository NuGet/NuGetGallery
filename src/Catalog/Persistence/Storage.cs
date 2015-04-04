using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class Storage : IStorage
    {
        public Storage(Uri baseAddress)
        {
            string s = baseAddress.OriginalString.TrimEnd('/') + '/';
            BaseAddress = new Uri(s);
        }

        public override string ToString()
        {
            return BaseAddress.ToString();
        }

        protected abstract Task OnSave(Uri resourceUri, StorageContent content);
        protected abstract Task<StorageContent> OnLoad(Uri resourceUri);
        protected abstract Task OnDelete(Uri resourceUri);

        public async Task Save(Uri resourceUri, StorageContent content)
        {
            SaveCount++;

            TraceMethod("SAVE", resourceUri);

            try
            {
                await OnSave(resourceUri, content);
            }
            catch (Exception e)
            {
                string message = String.Format("SAVE EXCEPTION: {0} {1}", resourceUri, e.Message);
                Trace.WriteLine(message);
                throw new Exception(message, e);
            }
        }

        public async Task<StorageContent> Load(Uri resourceUri)
        {
            LoadCount++;

            TraceMethod("LOAD", resourceUri);

            try
            {
                return await OnLoad(resourceUri);
            }
            catch (Exception e)
            {
                string message = String.Format("LOAD EXCEPTION: {0} {1}", resourceUri, e.Message);
                Trace.WriteLine(message);
                throw new Exception(message, e);
            }
        }

        public async Task Delete(Uri resourceUri)
        {
            DeleteCount++;

            TraceMethod("DELETE", resourceUri);

            try
            {
                await OnDelete(resourceUri);
            }
            catch (Exception e)
            {
                string message = String.Format("DELETE EXCEPTION: {0} {1}", resourceUri, e.Message);
                Trace.WriteLine(message);
                throw new Exception(message, e);
            }
        }

        public async Task<string> LoadString(Uri resourceUri)
        {
            StorageContent content = await Load(resourceUri);
            if (content == null)
            {
                return null;
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    StreamReader reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }
            }
        }

        public Uri BaseAddress { get; private set; }
        public abstract bool Exists(string fileName);

        public bool Verbose
        {
            get;
            set;
        }

        public int SaveCount
        {
            get;
            protected set;
        }

        public int LoadCount
        {
            get;
            protected set;
        }

        public int DeleteCount
        {
            get;
            protected set;
        }

        public void ResetStatistics()
        {
            SaveCount = 0;
            LoadCount = 0;
            DeleteCount = 0;
        }

        public Uri ResolveUri(string relativeUri)
        {
            return new Uri(BaseAddress, relativeUri);
        }

        protected string GetName(Uri uri)
        {
            string address = Uri.UnescapeDataString(BaseAddress.GetLeftPart(UriPartial.Path));
            if (!address.EndsWith("/"))
            {
                address += "/";
            }
            string s = uri.ToString();
            string name = s.Substring(address.Length);
            return name;
        }

        protected void TraceMethod(string method, Uri resourceUri)
        {
            if (Verbose)
            {
                Trace.WriteLine(String.Format("{0} {1}", method, resourceUri));
            }
        }
    }
}
