using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GatherMergeRewrite
{
    class Program
    {
        static void Upload(string ownerId, string registrationId, string nupkg, DateTime published)
        {
            UploadData data = new UploadData
            {
                OwnerId = ownerId,
                RegistrationId = registrationId,
                Nupkg = nupkg,
                Published = published
            };

            Processor.Upload(data);
        }

        static void Main(string[] args)
        {
            Config.Container = "pub";
            Config.BaseAddress = "http://nuget3.blob.core.windows.net";
            Config.ConnectionString = "";

            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.0.4.1.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.0.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.0.5.1.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.0.6.0.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.0.7.0.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.0.8.0.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.0.8.2.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.0.9.0.2110.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.1.0.0.2473.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.1.0.1-portablerc1.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.1.0.1-rc2.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.1.0.2.2880.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotnetrdf.1.0.3-prerelease.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\resolver\dotNetRDF.1.0.3.nupkg", DateTime.Now);

            //Upload("microsoft", "htmlagilitypack", @"c:\data\resolver\htmlagilitypack.1.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "htmlagilitypack", @"c:\data\resolver\htmlagilitypack.1.4.0.nupkg", DateTime.Now);
            //Upload("microsoft", "htmlagilitypack", @"c:\data\resolver\htmlagilitypack.1.4.1.nupkg", DateTime.Now);
            //Upload("microsoft", "htmlagilitypack", @"c:\data\resolver\htmlagilitypack.1.4.2.nupkg", DateTime.Now);
            //Upload("microsoft", "htmlagilitypack", @"c:\data\resolver\htmlagilitypack.1.4.3.nupkg", DateTime.Now);
            //Upload("microsoft", "htmlagilitypack", @"c:\data\resolver\htmlagilitypack.1.4.4.nupkg", DateTime.Now);
            //Upload("microsoft", "htmlagilitypack", @"c:\data\resolver\htmlagilitypack.1.4.5.nupkg", DateTime.Now);
            //Upload("microsoft", "htmlagilitypack", @"c:\data\resolver\HtmlAgilityPack.1.4.6.nupkg", DateTime.Now);

            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.3.5.8.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.0.3.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.0.4.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.0.5.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.0.6.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.0.7.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.0.8.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.5.1.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.5.2.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\newtonsoft.json.4.5.3.nupkg", DateTime.Now);
            //Upload("microsoft", "newtonsoft.json", @"c:\data\resolver\Newtonsoft.Json.5.0.8.nupkg", DateTime.Now);

            //Upload("microsoft", "vds.common", @"c:\data\resolver\vds.common.0.9.0.nupkg", DateTime.Now);
            //Upload("microsoft", "vds.common", @"c:\data\resolver\vds.common.1.0.0.nupkg", DateTime.Now);
            //Upload("microsoft", "vds.common", @"c:\data\resolver\vds.common.1.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "vds.common", @"c:\data\resolver\vds.common.1.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "vds.common", @"c:\data\resolver\vds.common.1.1.0.nupkg", DateTime.Now);
            //Upload("microsoft", "vds.common", @"c:\data\resolver\vds.common.1.1.1.nupkg", DateTime.Now);
            //Upload("microsoft", "vds.common", @"c:\data\resolver\vds.common.1.1.2.nupkg", DateTime.Now);
            //Upload("microsoft", "vds.common", @"c:\data\resolver\vds.common.1.1.3.nupkg", DateTime.Now);
            //Upload("microsoft", "vds.common", @"c:\data\resolver\VDS.Common.1.2.0.nupkg", DateTime.Now);

            //Upload("microsoft", "json-ld.net", @"c:\data\resolver\json-ld.net.1.0.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "json-ld.net", @"c:\data\resolver\json-ld.net.1.0.0-rc2.nupkg", DateTime.Now);
            //Upload("microsoft", "json-ld.net", @"c:\data\resolver\json-ld.net.1.0.0-rc3.nupkg", DateTime.Now);
            //Upload("microsoft", "json-ld.net", @"c:\data\resolver\json-ld.net.1.0.0-rc4.nupkg", DateTime.Now);
            //Upload("microsoft", "json-ld.net", @"c:\data\resolver\json-ld.net.1.0.0-rc5.nupkg", DateTime.Now);
            //Upload("microsoft", "json-ld.net", @"c:\data\resolver\json-ld.net.1.0.0.nupkg", DateTime.Now);

            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.0.0.50403.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.0.1-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.0.2-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.1.0-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.1.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.1.0-rc2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.1.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.2.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.2.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.3.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.4.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.4.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.5.0-alpha1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.5.0-alpha2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.5.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.6.0-alpha1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.6.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.6.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.edm", @"c:\data\resolver\microsoft.data.edm.5.6.1.nupkg", DateTime.Now);

            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.0.0.50403.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.0.1-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.0.2-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.1.0-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.1.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.1.0-rc2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.1.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.2.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.2.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.3.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.4.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.4.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.5.0-alpha1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.5.0-alpha2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.5.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.6.0-alpha1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.6.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.6.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.odata", @"c:\data\resolver\microsoft.data.odata.5.6.1.nupkg", DateTime.Now);

            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.0.0.50403.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.0.1-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.0.2-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.1.0-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.1.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.1.0-rc2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.1.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.2.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.2.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.3.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.4.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.4.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.5.0-alpha1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.5.0-alpha2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.5.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.6.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.6.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.data.services.client", @"c:\data\resolver\microsoft.data.services.client.5.6.1.nupkg", DateTime.Now);

            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.1.7.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.1.7.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.1.7.0.3.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.1.7.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.1.8.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.2.0.0.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.2.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.2.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "microsoft.windowsazure.configurationmanager", @"c:\data\resolver\microsoft.windowsazure.configurationmanager.2.0.3.nupkg", DateTime.Now);

            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.0.0.50403.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.0.1-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.0.2-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.1.0-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.1.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.1.0-rc2.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.1.0.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.2.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.2.0.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.3.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.4.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.4.0.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.5.0-alpha1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.5.0-alpha2.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.5.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.6.0-alpha1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.6.0-rc1.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.6.0.nupkg", DateTime.Now);
            //Upload("microsoft", "system.spatial", @"c:\data\resolver\system.spatial.5.6.1.nupkg", DateTime.Now);

            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.1.0.0.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.1.6.0.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.1.7.0.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.0.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.3.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.4.1.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.4.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.5.1.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.5.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.6.1.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.0.6.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.1.0-rc.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.1.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.1.0.3.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.1.0.4.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.2.1.0.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.3.0.0.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.3.0.1.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.3.0.2.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.3.0.3.nupkg", DateTime.Now);
            //Upload("microsoft", "windowsazure.storage", @"c:\data\resolver\windowsazure.storage.3.1.0.nupkg", DateTime.Now);

            //Upload("microsoft", "aspnet.suppressformsredirect", @"c:\data\resolver\aspnet.suppressformsredirect.0.0.1.1.nupkg", DateTime.Now);
            //Upload("microsoft", "aspnet.suppressformsredirect", @"c:\data\resolver\aspnet.suppressformsredirect.0.0.1.2.nupkg", DateTime.Now);
            //Upload("microsoft", "aspnet.suppressformsredirect", @"c:\data\resolver\aspnet.suppressformsredirect.0.0.1.4.nupkg", DateTime.Now);

            //Upload("microsoft", "httpclient", @"c:\data\resolver\httpclient.0.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "httpclient", @"c:\data\resolver\httpclient.0.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "httpclient", @"c:\data\resolver\httpclient.0.6.0.nupkg", DateTime.Now);

            //Upload("microsoft", "jsonvalue", @"c:\data\resolver\jsonvalue.0.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "jsonvalue", @"c:\data\resolver\jsonvalue.0.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "jsonvalue", @"c:\data\resolver\jsonvalue.0.6.0.nupkg", DateTime.Now);

            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.0.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.1.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.2.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.3.1.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.3.2.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.4.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.4.1.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.4.2.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.4.3.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.4.4.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.5.1.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.5.2.nupkg", DateTime.Now);
            //Upload("microsoft", "webactivator", @"c:\data\resolver\webactivator.1.5.3.nupkg", DateTime.Now);

            //Upload("microsoft", "webapi", @"c:\data\resolver\webapi.0.6.0.nupkg", DateTime.Now);

            //Upload("microsoft", "webapi.all", @"c:\data\resolver\webapi.all.0.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webapi.all", @"c:\data\resolver\webapi.all.0.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webapi.all", @"c:\data\resolver\webapi.all.0.6.0.nupkg", DateTime.Now);

            //Upload("microsoft", "webapi.core", @"c:\data\resolver\webapi.core.0.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webapi.core", @"c:\data\resolver\webapi.core.0.5.0.nupkg", DateTime.Now);

            //Upload("microsoft", "webapi.enhancements", @"c:\data\resolver\webapi.enhancements.0.3.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webapi.enhancements", @"c:\data\resolver\webapi.enhancements.0.6.0.nupkg", DateTime.Now);

            //Upload("microsoft", "webapi.odata", @"c:\data\resolver\webapi.odata.0.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "webapi.odata", @"c:\data\resolver\webapi.odata.0.6.0.nupkg", DateTime.Now);

            //Upload("microsoft", "microsoft.web.infrastructure", @"c:\data\resolver\microsoft.web.infrastructure.1.0.0.nupkg", DateTime.Now);
        }
    }
}
