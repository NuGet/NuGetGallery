using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Catalog
{
    public class CloudPackageHandle : PackageHandle
    {
        Stream _stream;
        List<string> _owners;
        string _registrationId;
        DateTime _published;

        public string RegistrationId
        {
            get
            {
                return _registrationId;
            }
        }

        public CloudPackageHandle(Stream stream, List<string> owners, string registrationId, DateTime published)
        {
            _stream = stream;
            _owners = owners;
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
                OwnerIds = _owners,
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
