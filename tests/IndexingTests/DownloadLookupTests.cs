using Newtonsoft.Json;
using NuGet.Indexing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace IndexingTests
{
    public class DownloadLookupTests
    {
        [Theory]
        [InlineData(@"[[""AutofacContrib.NSubstitute"",[""2.4.3.700"",406],[""2.5.0"",137]],[""Assman.Core"",[""2.0.7"",138]]]")]
        public void DownloadLookUp_ValidJson(string json)
        {
            StringDownloadLookup downloadLookup = new StringDownloadLookup(json);
            IDictionary<string,IDictionary<string,int>> packages = downloadLookup.Load();
            Assert.True(packages != null & packages.Count() == 2);
            IDictionary<string,int> versions = new Dictionary<string,int>();
            Assert.True(packages.TryGetValue("AutofacContrib.NSubstitute".ToLowerInvariant(), out versions));
            Assert.True(versions.Count() == 2);
            Assert.True(versions.Values.ToArray()[0] == 406 && versions.Values.ToArray()[1] == 137);
        }

        [Theory]
        [InlineData(@"[[""AutofacContrib.NSubstitute"",[""2.4.3.700"",406],[""2.5.0"",137]],[""AutofacContrib.NSubstitute"",[""3"",0],[""2.5"",0]]]")]
        public void DownloadLookUp_NoExceptionThrownWithDeuplicateKeys(string json)
        {
            StringDownloadLookup downloadLookup = new StringDownloadLookup(json);
            IDictionary<string, IDictionary<string, int>> packages = downloadLookup.Load();
            // Dict should have only one entry when there are duplicate entries.
            Assert.True(packages != null & packages.Count() == 1);
            IDictionary<string, int> versions = new Dictionary<string, int>();
            Assert.True(packages.TryGetValue("AutofacContrib.NSubstitute".ToLowerInvariant(), out versions));
            Assert.True(versions.Count() == 2);
            // When duplicate entries are present,the first one should be taken and the consecutive ones should be ignored.
            Assert.True(versions.Values.ToArray()[0] == 406 && versions.Values.ToArray()[1] == 137);
        }

        [Theory]
        [InlineData(@"[[""AutofacContrib.NSubstitute"",[2.4.3.700,406],[""2.5.0"",137]],[""Assman.Core"",[""2.0.7"",138]]]")]
        public void DownloadLookUp_InvalidEntriesIgnored(string json)
        {
            StringDownloadLookup downloadLookup = new StringDownloadLookup(json);
            IDictionary<string, IDictionary<string, int>> packages = downloadLookup.Load();
            // Out of two entries one is invalid. So dict count should be 1.
            Assert.True(packages != null & packages.Count() == 1);
            IDictionary<string, int> versions = new Dictionary<string, int>();
            Assert.False(packages.TryGetValue("AutofacContrib.NSubstitute".ToLowerInvariant(), out versions));
            Assert.True(packages.TryGetValue("Assman.Core".ToLowerInvariant(), out versions));
            Assert.True(versions.Count() == 1);
            // When duplicate entries are present,the first one should be taken and the consecutive ones should be ignored.
            Assert.True(versions.Values.ToArray()[0] == 138 && versions.Keys.ToArray()[0] == "2.0.7");
        }

        [Theory]
        [InlineData(@"[WDSD$%^")]
        public void DownloadLookUp_InvalidJsonDoesntThrowsException(string json)
        {
            StringDownloadLookup downloadLookup = new StringDownloadLookup(json);
            IDictionary<string, IDictionary<string, int>> packages = downloadLookup.Load();
            // Invaid json should return empty dictionary.
            Assert.True(packages != null & packages.Count() == 0);         
        }
    }

    public class StringDownloadLookup : DownloadLookup
    {
        string _path;

        public override string Path { get { return _path; } }

        public StringDownloadLookup(string json)
        {
            _path = json;
        }
        protected override JsonReader GetReader()
        {
            return new JsonTextReader(new StringReader(Path));
        }
    }
}
