using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Catalog
{
    public class CloudPackageHandle : PackageHandle
    {
        Stream _stream;
        string _owner;
        string _registrationId;
        DateTime _published;

        public string RegistrationId
        {
            get
            {
                return _registrationId;
            }
        }

        public CloudPackageHandle(Stream stream, string owner, string registrationId, DateTime published)
        {
            _stream = stream;
            _owner = owner;
            _registrationId = registrationId;
            _published = published;
        }

        public override Task<PackageData> GetData()
        {
            ZipArchive package = Utils.GetPackage(_stream);
            XDocument nuspec = Utils.GetNuspec(package);

            if (nuspec == null) throw new NuspecMissingException();

            PackageData result = new PackageData()
            {
                OwnerId = _owner,
                RegistrationId = _registrationId,
                Published = _published,
                Nuspec = nuspec
            };

            return Task.FromResult(result);
        }

        public void Close()
        {
            _stream.Close();
        }
    }
}
