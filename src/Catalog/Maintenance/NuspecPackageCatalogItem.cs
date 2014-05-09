using System.IO;
using System.Xml.Linq;

namespace Catalog.Maintenance
{
    public class NuspecPackageCatalogItem : PackageCatalogItem
    {
        string _name;
        string _fullName;

        public NuspecPackageCatalogItem(FileInfo fileInfo)
        {
            _name = fileInfo.Name.Substring(0, fileInfo.Name.Length - 4).ToLowerInvariant();
            _fullName = fileInfo.FullName;
        }

        protected override string GetItemName()
        {
            return _name;
        }

        protected override XDocument GetNuspec()
        {
            using (StreamReader reader = new StreamReader(_fullName))
            {
                return XDocument.Load(reader);
            }
        }
    }
}
