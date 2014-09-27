using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public abstract class CatalogItem
    {
        DateTime _timeStamp;
        Uri _baseAddress;
        Guid _commitId;

        public void SetTimeStamp(DateTime timeStamp)
        {
            _timeStamp = timeStamp;
        }

        public void SetCommitId(Guid commitId)
        {
            _commitId = commitId;
        }

        public void SetBaseAddress(Uri baseAddress)
        {
            _baseAddress = baseAddress;
        }

        public abstract StorageContent CreateContent(CatalogContext context);

        public abstract Uri GetItemType();

        public virtual IGraph CreatePageContent(CatalogContext context)
        {
            return null;
        }

        protected abstract string GetItemIdentity();

        public Uri GetBaseAddress()
        {
            return new Uri(_baseAddress, "data/" + MakeTimeStampPathComponent(_timeStamp));
        }

        public string GetRelativeAddress()
        {
            return GetItemIdentity() + ".json";
        }

        public virtual Uri GetItemAddress()
        {
            return new Uri(GetBaseAddress(), GetRelativeAddress());
        }

        protected DateTime GetTimeStamp()
        {
            return _timeStamp;
        }

        protected Guid GetCommitId()
        {
            return _commitId;
        }

        protected static string MakeTimeStampPathComponent(DateTime timeStamp)
        {
            return string.Format("{0:0000}.{1:00}.{2:00}.{3:00}.{4:00}.{5:00}/", timeStamp.Year, timeStamp.Month, timeStamp.Day, timeStamp.Hour, timeStamp.Minute, timeStamp.Second);
        }
    }
}
