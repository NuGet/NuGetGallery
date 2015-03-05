using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Management.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NuGet.Services.Publish;

namespace PowerShellPublishTests
{
    [TestClass]
    public class ExtractMetadataTests
    {
        private const string TestManifestPath = "testManifest.psd1";
        private const string TestEmptyManifestPath = "testEmptyManifest.psd1";
        private const string TestCorruptedManifestPath = "testCorruptedManifest.psd1";
        private const string TestInvalidManifestPath = "testInvalidManifest.psd1";
        private const string TestMissingRequiredFieldsPath = "testMissingRequiredFields.psd1";

        [TestMethod]
        public void TestExtractingMetadata()
        {
            var f = File.Open(TestManifestPath, FileMode.Open);

            IRegistrationOwnership registrationOwnership = new MockIRegistrationOwnership();
            PowerShellPublishImpl pub = new PowerShellPublishImpl(registrationOwnership);

            PrivateObject pubPrivateObject = new PrivateObject(pub);
            var result = pubPrivateObject.Invoke("CreateMetadataObject", "", (Stream)f) as JObject;

            Assert.IsNotNull(result);
            Assert.AreEqual(1, (result.GetValue("authors") as JArray)?.Count);
            Assert.AreEqual("Darek", result.GetValue("authors")[0].Value<string>());
            Assert.AreEqual("1.0", result.GetValue("ModuleVersion"));
            Assert.AreEqual("Microsoft", result.GetValue("CompanyName"));
            Assert.AreEqual("e4da48d8-20df-4d58-bfa6-2e54486fca5b", result.GetValue("GUID"));
            Assert.AreEqual(null, result.GetValue("PowerShellHostVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("DotNetFrameworkVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("CLRVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("ProcessorArchitecture").Value<string>());
            Assert.AreEqual(2, (result.GetValue("CmdletsToExport") as JArray)?.Count);
            Assert.AreEqual("Get-Test", result.GetValue("CmdletsToExport")[0].Value<string>());
            Assert.AreEqual("Set-Test", result.GetValue("CmdletsToExport")[1].Value<string>());
            Assert.AreEqual(1, (result.GetValue("FunctionsToExport") as JArray)?.Count);
            Assert.AreEqual("*", result.GetValue("FunctionsToExport")[0].Value<string>());
            Assert.AreEqual(0, (result.GetValue("DscResourcesToExport") as JArray)?.Count);
            Assert.AreEqual("http://this.is.test.license.com", result.GetValue("licenseUrl"));
            Assert.AreEqual(null, result.GetValue("iconUrl").Value<string>());
            Assert.AreEqual(1, (result.GetValue("tags") as JArray)?.Count);
            Assert.AreEqual("tag1", result.GetValue("tags")[0].Value<string>());
            Assert.AreEqual("http://github.com/TestModule", result.GetValue("projectUrl"));
            Assert.AreEqual("This is our best release so far.", result.GetValue("releaseNotes"));
        }

        [TestMethod]
        public void TestExtractingMetadataEmptyFile()
        {
            var f = File.Open(TestEmptyManifestPath, FileMode.Open);

            IRegistrationOwnership registrationOwnership = new MockIRegistrationOwnership();
            PowerShellPublishImpl pub = new PowerShellPublishImpl(registrationOwnership);

            PrivateObject pubPrivateObject = new PrivateObject(pub);
            var result = pubPrivateObject.Invoke("CreateMetadataObject", "", (Stream)f) as JObject;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, (result.GetValue("authors") as JArray)?.Count);
            Assert.AreEqual(null, result.GetValue("ModuleVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("CompanyName").Value<string>());
            Assert.AreEqual(null, result.GetValue("GUID").Value<string>());
            Assert.AreEqual(null, result.GetValue("PowerShellHostVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("DotNetFrameworkVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("CLRVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("ProcessorArchitecture").Value<string>());
            Assert.AreEqual(0, (result.GetValue("CmdletsToExport") as JArray)?.Count);
            Assert.AreEqual(0, (result.GetValue("FunctionsToExport") as JArray)?.Count);
            Assert.AreEqual(0, (result.GetValue("DscResourcesToExport") as JArray)?.Count);
            Assert.AreEqual(null, result.GetValue("licenseUrl").Value<string>());
            Assert.AreEqual(null, result.GetValue("iconUrl").Value<string>());
            Assert.AreEqual(0, (result.GetValue("tags") as JArray)?.Count);
            Assert.AreEqual(null, result.GetValue("projectUrl").Value<string>());
            Assert.AreEqual(null, result.GetValue("releaseNotes").Value<string>());
        }

        [TestMethod]
        public void TestIsMetadataFile()
        {
            IRegistrationOwnership registrationOwnership = new MockIRegistrationOwnership();
            PowerShellPublishImpl pub = new PowerShellPublishImpl(registrationOwnership);

            PrivateObject pubPrivateObject = new PrivateObject(pub);
            Assert.AreEqual(true, pubPrivateObject.Invoke("IsMetadataFile", "test.psd1") as bool?);
            Assert.AreEqual(false, pubPrivateObject.Invoke("IsMetadataFile", "test.psm1") as bool?);
            Assert.AreEqual(false, pubPrivateObject.Invoke("IsMetadataFile", "subdir/test.psd1") as bool?);
        }

        [TestMethod]
        public void TestCorruptedMetadata()
        {
            var f = File.Open(TestCorruptedManifestPath, FileMode.Open);

            IRegistrationOwnership registrationOwnership = new MockIRegistrationOwnership();
            PowerShellPublishImpl pub = new PowerShellPublishImpl(registrationOwnership);

            PrivateObject pubPrivateObject = new PrivateObject(pub);

            pubPrivateObject.Invoke("IsMetadataFile", TestCorruptedManifestPath);

            pubPrivateObject.Invoke("CreateMetadataObject", "", (Stream)f);

            var result = pubPrivateObject.Invoke("Validate", new object[1]{null}) as List<string>;

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Unexpected element encountered.", result[0]);
        }

        [TestMethod]
        public void TestInvalidMetadata()
        {
            var f = File.Open(TestInvalidManifestPath, FileMode.Open);

            IRegistrationOwnership registrationOwnership = new MockIRegistrationOwnership();
            PowerShellPublishImpl pub = new PowerShellPublishImpl(registrationOwnership);

            PrivateObject pubPrivateObject = new PrivateObject(pub);
            var result = pubPrivateObject.Invoke("CreateMetadataObject", "", (Stream)f) as JObject;

            Assert.IsNotNull(result);
            Assert.AreEqual(1, (result.GetValue("authors") as JArray)?.Count);
            Assert.AreEqual("Darek", result.GetValue("authors")[0].Value<string>());
            Assert.AreEqual("1.0", result.GetValue("ModuleVersion"));
            Assert.AreEqual("Microsoft", result.GetValue("CompanyName"));
            Assert.AreEqual("e4da48d8-20df-4d58-bfa6-2e54486fca5b", result.GetValue("GUID"));
            Assert.AreEqual(null, result.GetValue("PowerShellHostVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("DotNetFrameworkVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("CLRVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("ProcessorArchitecture").Value<string>());
            Assert.AreEqual(2, (result.GetValue("CmdletsToExport") as JArray)?.Count);
            Assert.AreEqual("Get-Test", result.GetValue("CmdletsToExport")[0].Value<string>());
            Assert.AreEqual("Set-Test", result.GetValue("CmdletsToExport")[1].Value<string>());
            Assert.AreEqual(1, (result.GetValue("FunctionsToExport") as JArray)?.Count);
            Assert.AreEqual("*", result.GetValue("FunctionsToExport")[0].Value<string>());
            Assert.AreEqual(0, (result.GetValue("DscResourcesToExport") as JArray)?.Count);
            Assert.AreEqual("http://this.is.test.license.com", result.GetValue("licenseUrl"));
            Assert.AreEqual(null, result.GetValue("iconUrl").Value<string>());
            Assert.AreEqual(1, (result.GetValue("tags") as JArray)?.Count);
            Assert.AreEqual("tag1", result.GetValue("tags")[0].Value<string>());
            Assert.AreEqual("http://github.com/TestModule", result.GetValue("projectUrl"));
            Assert.AreEqual("This is our best release so far.", result.GetValue("releaseNotes"));
        }

        [TestMethod]
        public void TestMissingRequiredFieldsMetadata()
        {
            var f = File.Open(TestMissingRequiredFieldsPath, FileMode.Open);

            IRegistrationOwnership registrationOwnership = new MockIRegistrationOwnership();
            PowerShellPublishImpl pub = new PowerShellPublishImpl(registrationOwnership);

            PrivateObject pubPrivateObject = new PrivateObject(pub);

            pubPrivateObject.Invoke("IsMetadataFile", TestMissingRequiredFieldsPath);

            var result = pubPrivateObject.Invoke("CreateMetadataObject", "", (Stream)f) as JObject;

            Assert.IsNotNull(result);
            Assert.AreEqual("Microsoft", result.GetValue("CompanyName"));
            Assert.AreEqual(null, result.GetValue("PowerShellHostVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("DotNetFrameworkVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("CLRVersion").Value<string>());
            Assert.AreEqual(null, result.GetValue("ProcessorArchitecture").Value<string>());
            Assert.AreEqual(2, (result.GetValue("CmdletsToExport") as JArray)?.Count);
            Assert.AreEqual("Get-Test", result.GetValue("CmdletsToExport")[0].Value<string>());
            Assert.AreEqual("Set-Test", result.GetValue("CmdletsToExport")[1].Value<string>());
            Assert.AreEqual(1, (result.GetValue("FunctionsToExport") as JArray)?.Count);
            Assert.AreEqual("*", result.GetValue("FunctionsToExport")[0].Value<string>());
            Assert.AreEqual(0, (result.GetValue("DscResourcesToExport") as JArray)?.Count);
            Assert.AreEqual("http://this.is.test.license.com", result.GetValue("licenseUrl"));
            Assert.AreEqual(null, result.GetValue("iconUrl").Value<string>());
            Assert.AreEqual(1, (result.GetValue("tags") as JArray)?.Count);
            Assert.AreEqual("tag1", result.GetValue("tags")[0].Value<string>());
            Assert.AreEqual("http://github.com/TestModule", result.GetValue("projectUrl"));
            Assert.AreEqual("This is our best release so far.", result.GetValue("releaseNotes"));

            var validationMessage = pubPrivateObject.Invoke("Validate", new object[1] { null }) as List<string>;

            Assert.IsNotNull(validationMessage);
            Assert.AreEqual(1, validationMessage.Count);
            Assert.IsTrue(validationMessage[0].Contains("Author"));
            Assert.IsTrue(validationMessage[0].Contains("ModuleVersion"));
            Assert.IsTrue(validationMessage[0].Contains("GUID"));
        }
    }
}
