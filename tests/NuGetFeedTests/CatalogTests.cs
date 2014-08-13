using Newtonsoft.Json;
using NuGet;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGetFeed;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using Xunit;

namespace NuGetFeedTests
{
    public class CatalogTests : FunctionalTests
    {
        [Fact]
        public void CatalogTest_Single_FilesExist()
        {
            string pathA = CreateNupkg("a", "1.0.0");

            CatalogStep step = new CatalogStep(Config, new string[] { pathA });
            step.Run();

            var files = Config.Catalog.LocalFolder.GetFiles("*.json", SearchOption.AllDirectories);

            Assert.True(files.Any(f => f.Name == "index.json"));
            Assert.True(files.Any(f => f.Name == "page0.json"));
            Assert.True(!files.Any(f => f.Name == "page1.json"));
            Assert.True(files.Any(f => f.Name == "a.1.0.0.json"));
        }

        [Fact]
        public void CatalogTest_Multiple_FilesExist()
        {
            var nupkgs = GenPackages(15);

            CatalogStep step = new CatalogStep(Config, nupkgs, 2);
            step.Run();

            var files = Config.Catalog.LocalFolder.GetFiles("*.json", SearchOption.AllDirectories);

            Assert.True(files.Any(f => f.Name == "index.json"));
            Assert.True(files.Any(f => f.Name == "page0.json"));
            Assert.True(!files.Any(f => f.Name == "page1.json"));
            Assert.True(files.Any(f => f.Name == "test0x.1.0.0.json"));
            Assert.True(files.Any(f => f.Name == "test14x.1.14.0.json"));
            Assert.True(files.Any(f => f.Name == "test7x.1.7.0.json"));
        }

        [Fact]
        public void CatalogTest_VerifyPackage()
        {
            string pathA = CreateNupkg("MyPackage", "1.2.1");

            CatalogStep step = new CatalogStep(Config, new string[] { pathA });
            step.Run();

            var file = Config.Catalog.LocalFolder.GetFiles("mypackage.1.2.1.json", SearchOption.AllDirectories).FirstOrDefault();

            JsonTextReader reader = new JsonTextReader(file.OpenText());

            CollectorHttpClient client = new CollectorHttpClient(Config.Catalog.FileSystemEmulator);

            var task = client.GetGraphAsync(new Uri(Config.Catalog.BaseAddress.AbsoluteUri + "/index.json"));
            task.Wait();

            IGraph graph = task.Result;
        }

        private List<string> GenPackages(int count)
        {
            List<string> packages = new List<string>();

            for (int i=0; i < count; i++)
            {
                var package = CreateNupkg(String.Format("test{0}x", i), String.Format("1.{0}.0", i));
                packages.Add(package);
            }

            return packages;
        }
    }
}
