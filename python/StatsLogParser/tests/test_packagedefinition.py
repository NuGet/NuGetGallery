import pytest

from loginterpretation.packagedefinition import PackageDefinition

@pytest.mark.parametrize("expected_package_id,expected_package_version,request_url", [
    ("nuget.core", "1.7.0.1540", "http://localhost/packages/nuget.core.1.7.0.1540.nupkg"),
    ("nuget.core", "1.7.0.1540", "http://localhost/packages/nuget.core.1.7.0.1540.nupkg?packageVersion=1.7.0.1540"),
    ("nuget.core", "1.0.1-beta1", "http://localhost/packages/nuget.core.1.0.1-beta1.nupkg"),
    ("nuget.core", "1.0.1-beta1-build1234", "http://localhost/packages/nuget.core.1.0.1-beta1-build1234.nupkg"),
    ("nuget.core", "1.0.1-beta1-build1234", "http://localhost/packages/nuget.core.1.0.1-beta1-build1234.nupkg?packageVersion=1.0.1-beta1-build1234"),
    ("nuget.core", "1.0.1-beta1.1", "http://localhost/packages/nuget.core.1.0.1-beta1.1.nupkg?packageVersion=1.0.1-beta1.1"),
    ("nuget.core", "1.7.0.1540", "http://localhost/packages/nuget.core.1.7.0.1540.nupkg"),
    ("nuget.core", "1.7.0.1540", "http://localhost/packages/nuget.core.1.7.0.1540.nupkg?packageVersion=1.7.0.1540"),
    ("nuget.core", "1.0.1", "http://localhost/packages/nuget.core.1.0.1.nupkg?packageVersion=1.0.1"),
    ("1", "1.0.0", "http://localhost/packages/1.1.0.0.nupkg"),
    ("1", "1.0.0", "http://localhost/packages/1.1.0.0.nupkg?packageVersion=1.0.0"),
    ("dnx-mono", "1.0.0-beta7", "http://localhost/packages/dnx-mono.1.0.0-beta7.nupkg"),
    ("dnx-mono", "1.0.0-beta7", "http://localhost/packages/dnx-mono.1.0.0-beta7.nupkg?packageVersion=1.0.0-beta7"),
    ("Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus", "6.0.1304", "http://localhost/packages/Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus.6.0.1304.nupkg"),
    ("Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus", "6.0.1304", "http://localhost/packages/Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus.6.0.1304.nupkg?packageVersion=6.0.1304"),
    ("新包", "1.0.0", "http://localhost/packages/%E6%96%B0%E5%8C%85.1.0.0.nupkg"),
    ("新包", "1.0.0", "http://localhost/packages/%E6%96%B0%E5%8C%85.1.0.0.nupkg?packageVersion=1.0.0"),
    ("microsoft.applicationinsights.dependencycollector", "2.4.1", "http://localhost/packages/microsoft.applicationinsights.dependencycollector%20.2.4.1.nupkg"),
    ("microsoft.applicationinsights.dependencycollector", "2.4.1", "http://localhost/packages/microsoft.applicationinsights.dependencycollector%20.2.4.1.nupkg?packageVersion=2.4.1"),
    ("xunit", "2.4.0-beta.1.build3958", "http://localhost/packages/xunit.2.4.0-beta.1.build3958.nupkg"),
    ("xunit.1", "2.4.1-beta.1.build3958", "http://localhost/packages/xunit.1.2.4.1-beta.1.build3958.nupkg?packageVersion=2.4.1-beta.1.build3958"),
    ("xunit.1", "2.4.1", "http://localhost/packages/xunit.1.2.4.1.nupkg?packageVersion=2.4.1"),
    ("5.0.0.0", "5.0.0", "http://localhost/packages/5.0.0.0.5.0.0.nupkg"),
    ("5.0.0.0", "5.0.0", "http://localhost/packages/5.0.0.0.5.0.0.nupkg?packageVersion=5.0.0"),
    ("xunit.1", "2.4.1", "https://api.nuget.org/v3-flatcontainer/xunit.1/2.4.1/xunit.1.2.4.1.nupkg"),
    ("1", "2.3.4", "http://localhost/packages/1.2.3.4.nupkg"),
    ("1", "2.3.4.5", "http://localhost/packages/1.2.3.4.5.nupkg"),
    ("1.2", "3.4.5.6", "http://localhost/packages/1.2.3.4.5.6.nupkg"),
    ("1.2.3", "4.5.6", "http://localhost/packages/1.2.3.4.5.6.nupkg?packageVersion=4.5.6"),
    ("1.2", "3.4.5.6", "http://localhost/packages/1.2.3.4.5.6.nupkg?packageVersion=3.4.5.6")
    ])
def test_from_request_url(expected_package_id, expected_package_version, request_url):
    found =  PackageDefinition.from_request_url(request_url)
    assert found and found[0] == PackageDefinition(expected_package_id, expected_package_version)

@pytest.mark.parametrize("request_url", [
    "http://localhost/packages/1.nupkg",
    "http://localhost/packages/1.2.nupkg",
    "http://localhost/packages/1.2.3.exe"
    ])
def test_from_request_url_returns_none_when_invalid_url(request_url):
    found = PackageDefinition.from_nuget_exe_url(request_url)
    assert not found


@pytest.mark.parametrize("expected_version,request_url", [
    ("5.9.1", "https://localhost/artifacts/win-x86-commandline/v5.9.1/nuget.exe"),
    ("5.8.0-preview.2", "https://localhost/artifacts/win-x86-commandline/v5.8.0-preview.2/nuget.exe"),
    ("3.5.0-beta2", "https://localhost/artifacts/win-x86-commandline/v3.5.0-beta2/nuget.exe"),
    ("latest", "https://localhost/artifacts/win-x86-commandline/latest/nuget.exe"),
    ("5.11.6", "/win-x86-commandline/v5.11.6/nuget.exe"),
    ("latest", "/win-x86-commandline/latest/nuget.exe")])
def test_from_nuget_exe_url_extracts_nuget_exe_version(expected_version, request_url):
    found =  PackageDefinition.from_nuget_exe_url(request_url)
    assert found and found.package_id == 'tool/nuget.exe' and found.package_version == expected_version

@pytest.mark.parametrize("request_url", [
    "",
    " ",
    "http://localhost/downloads/nuget.exe",
    "http://localhost/artifacts/win-x86-commandline/3.5.0/nuget.exe",
    "http://localhost/artifacts/win-x86-commandline/vlatest/nuget.exe",
    "http://localhost/artifacts/win-x86-commandline/v3.5.0/get.exe"
    ])
def test_from_nuget_exe_url_returns_none_when_invalid_url(request_url):
    found = PackageDefinition.from_nuget_exe_url(request_url)
    assert not found

# To invoke the pytest framework and run all tests
if __name__ == "__main__":
    pytest.main()
