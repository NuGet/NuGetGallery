using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Catalog
{
    public class LocalPackageHandle : PackageHandle
    {
        string _owner;
        string _registrationId;
        string _filename;
        DateTime _published;

        public LocalPackageHandle(string owner, string registrationId, string filename, DateTime published)
        {
            _owner = owner;
            _registrationId = registrationId;
            _filename = filename;
            _published = published;
        }

        public override async Task<PackageData> GetData()
        {
            Stream stream = new FileStream(_filename, FileMode.Open);
            ZipArchive package = Utils.GetPackage(stream);
            XDocument nuspec = Utils.GetNuspec(package);

            if (nuspec == null)
            {
                throw new ArgumentNullException(string.Format("{0} nuspec missing", _filename));
            }

            PackageData result = new PackageData()
            {
                OwnerId = _owner,
                RegistrationId = _registrationId,
                Published = _published,
                Nuspec = nuspec
            };

            return await Task<PackageData>.Factory.StartNew(() => { return result; });
        }
    }
}
