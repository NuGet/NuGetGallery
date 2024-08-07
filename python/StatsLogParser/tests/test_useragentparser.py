import sys
sys.path.append('..') # This is to add the parent directory to the path so that the module can be imported
from loginterpretation.useragentparser import UserAgentParser, UserAgent
import pytest


@pytest.mark.parametrize("user_agent,expected_client,expected_major,expected_minor,expected_patch", [
    ("NuGet Command Line/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Command Line", "1", "2", "3"),
    ("NuGet Command Line/2.8.50320.36 (Microsoft Windows NT 6.1.7601 Service Pack 1)", "NuGet Command Line", "2", "8", "50320"),
    ("NuGet xplat/3.4.0 (Microsoft Windows NT 6.2.9200.0)", "NuGet Cross-Platform Command Line", "3", "4", "0"),
    ("NuGet VS PowerShell Console/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet VS PowerShell Console", "1", "2", "3"),
    ("NuGet VS Packages Dialog/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet VS Packages Dialog - Solution", "1", "2", "3"),
    ("NuGet Add Package Dialog/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Add Package Dialog", "1", "2", "3"),
    ("NuGet Package Manager Console/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Package Manager Console", "1", "2", "3"),
    ("NuGet Visual Studio Extension/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Visual Studio Extension", "1", "2", "3"),
    ("Package-Installer/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Package-Installer", "1", "2", "3"),
    ("WebMatrix 1.2.3/4.5.6 (Microsoft Windows NT 6.2.9200.0)", "WebMatrix", "1", "2", "3"),
    ("NuGet Package Explorer Metro/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Package Explorer Metro", "1", "2", "3"),
    ("NuGet Package Explorer/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Package Explorer", "1", "2", "3"),
    ("JetBrains TeamCity 1.2.3 (Microsoft Windows NT 6.2.9200.0)", "JetBrains TeamCity", "1", "2", "3"),
    ("Nexus/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Sonatype Nexus", "1", "2", "3"),
    ("Artifactory/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "JFrog Artifactory", "1", "2", "3"),
    ("MyGet/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "MyGet", "1", "2", "3"),
    ("ProGet/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Inedo ProGet", "1", "2", "3"),
    ("Paket/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Paket", "1", "2", "3"),
    ("Paket", "Paket", None, None, None),
    ("Xamarin Studio/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Xamarin Studio", "1", "2", "3"),
    ("MonoDevelop/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "MonoDevelop", "1", "2", "3"),
    ("MonoDevelop-Unity/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "MonoDevelop", "1", "2", "3"),
    ("NuGet Core/2.8.50926.663 (Microsoft Windows NT 6.3.9600.0)", "NuGet", "2", "8", "50926"),
    ("NuGet/2.8.6", "NuGet", "2", "8", "6"),
    ("NuGet/3.0.0 (Microsoft Windows NT 6.3.9600.0)", "NuGet", "3", "0", "0"),
    ("Microsoft_.NET_Development_Utility/1.2.3-t150812191208 (Windows 6.2.9200.0)", "DNX Utility", "1", "2", "3"),
    ("NuGet Shim/3.0.51103.210 (Microsoft Windows NT 6.2.9200.0)", "NuGet Shim", "3", "0", "51103"),
    ("NuGet Client V3/3.0.0.0 (Microsoft Windows NT 10.0.10240.0, VS Enterprise/14.0)", "NuGet Client V3", "3", "0", "0"),
    ("NuGet Client V3/3.1.0.0 (Microsoft Windows NT 10.0.10240.0, VS Enterprise/14.0)", "NuGet Client V3", "3", "1", "0"),
    ("SharpDevelop/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "SharpDevelop", "1", "2", "3"),
    ("Mozilla/5.0 (Windows NT; Windows NT 6.2; en-US) WindowsPowerShell/5.0.9701.0", "Windows PowerShell", "5", "0", "9701"),
    ("Fiddler", "Fiddler", None, None, None),
    ("curl/7.21.0 (x86_64-pc-linux-gnu) libcurl/7.21.0 OpenSSL/0.9.8o zlib/1.2.3.4 libidn/1.18", "curl", "7", "21", "0"),
    ("Java/1.7.0_51", "Java", "1", "7", "0"),
    ("NuGet Core/2.8.6", "NuGet", "2", "8", "6"),
    ("NuGet Test Client/3.3.0", "NuGet Test Client", "3", "3", "0"),
    ("dotPeek/102.0.20150521.130901 (Microsoft Windows NT 6.3.9600.0; NuGet/2.8.60318.667; Wave/2.0.0; dotPeek/1.4.20150521.130901)", "JetBrains dotPeek", "1", "4", "20150521"),
    ("ReSharperPlatformVs10/102.0.20150521.123255 (Microsoft Windows NT 6.1.7601 Service Pack 1; NuGet/2.8.60318.667; Wave/2.0.0; ReSharper/9.1.20150521.134223; dotTrace/6.1.20150521.132011)", "JetBrains ReSharper Platform VS2010", "102", "0", "20150521"),
    ("ReSharperPlatformVs11/102.0.20150408.145317 (Microsoft Windows NT 6.2.9200.0; NuGet/2.8.50926.602; Wave/2.0.0; ReSharper/9.1.20150408.155143)", "JetBrains ReSharper Platform VS2012", "102", "0", "20150408"),
    ("ReSharperPlatformVs12/102.0.20150721.105606 (Microsoft Windows NT 6.3.9600.0; NuGet/2.8.60318.667; Wave/2.0.0; ReSharper/9.1.20150721.141555; dotTrace/6.1.20150721.135729; dotMemory/4.3.20150721.134307)", "JetBrains ReSharper Platform VS2013", "102", "0", "20150721"),
    ("ReSharperPlatformVs14/102.0.20150408.145317 (Microsoft Windows NT 10.0.10074.0; NuGet/2.8.50926.602; Wave/2.0.0; ReSharper/9.1.20150408.155143)", "JetBrains ReSharper Platform VS2015", "102", "0", "20150408"),
    ("NuGet MSBuild Task/4.0.0 (Microsoft Windows 10.0.15063)", "NuGet MSBuild Task", "4", "0", "0"),
    ("NuGet .NET Core MSBuild Task/4.4.0 (Microsoft Windows 10.0.15063)", "NuGet .NET Core MSBuild Task", "4", "4", "0"),
    ("NuGet Desktop MSBuild Task/4.4.0 (Microsoft Windows 10.0.15063)", "NuGet Desktop MSBuild Task", "4", "4", "0"),
    ("Cake NuGet Client/4.3.0 (Microsoft Windows 10.0.15063)", "Cake NuGet Client", "4", "3", "0"),
    ("Cake/2.3.0.0", "Cake", "2", "3", "0"),
    ("NuGet VS VSIX/4.3.0 (Microsoft Windows 10.0.15063)", "NuGet VS VSIX", "4", "3", "0"),
    ("NuGet+VS+VSIX/4.8.1+(Microsoft+Windows+NT+10.0.17134.0,+VS+Enterprise/15.0)", "NuGet VS VSIX", "4", "8", "1"),
    ("NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)", "NuGet Command Line", "4", "3", "0"),
    ("Mozilla/5.0 (Windows NT 10.0; Microsoft Windows 10.0.19045; en-US) PowerShell/7.2.9", "PowerShell Core", "7", "2", "9"),
    ("Xamarin Updater (Platform: osx-13.1.0) (Application: Visual Studio Community) (Version: 17.4.5 (build 8))", "Xamarin Updater", "17", "4", "5"),
    ("vsts-task-installer/0.12.0", "vsts-task-installer", "0", "12", "0"),
    ("Checkmarx-NugetShaCollector (abuse-sca@checkmarx.com)", "Checkmarx NugetShaCollector", None, None, None),
    ("Checkmarx-NugetSourceCodePriorityCollector (abuse-sca@checkmarx.com)", "Checkmarx NugetSourceCodePriorityCollector", None, None, None),
    ("Checkmarx-NugetDllShaCollector (abuse-sca@checkmarx.com)", "Checkmarx NugetDllShaCollector", None, None, None),
    ("Checkmarx-SourceCodeDownloader (abuse-sca@checkmarx.com)", "Checkmarx SourceCodeDownloader", None, None, None),
    ("AzureArtifacts/19.216.33401.7 (Microsoft Azure Artifacts [Azure DevOps,; Hosted)", "Azure artifacts", "19", "216", "33401"),
    ("Bazel/release 6.0.0", "Bazel", "6", "0", "0"),
    ("Visual Studio/6.4.0", "Visual Studio", "6", "4", "0"),
    ("NuGetMirror/6.0.0", "NuGetMirror", "6", "0", "0"),
    ("BaGet/1.0.0", "BaGet", "1", "0", "0")
    ])
def test_recognizes_custom_clients(user_agent, expected_client, expected_major, expected_minor, expected_patch):
    parsed = UserAgentParser.parse(user_agent)
    assert parsed == UserAgent(expected_client, expected_major, expected_minor, expected_patch)

# To invoke the pytest framework and run all tests
if __name__ == "__main__":
    pytest.main()
