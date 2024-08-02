import pytest
from unittest import mock
import builtins

from loginterpretation.packagedefinition import PackageDefinition

@pytest.mark.parametrize("expected_package_id,expected_package_version,request_url", [
    ("nuget.core", "1.7.0.1540", "http://localhost/packages/nuget.core.1.7.0.1540.nupkg"),
    ("nuget.core", "1.7.0.1540", "http://localhost/packages/nuget.core.1.7.0.1540.nupkg?packageVersion=1.7.0.1540"),
    ("nuget.core", "1.0.1-beta1", "http://localhost/packages/nuget.core.1.0.1-beta1.nupkg"),
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
    ("xunit.1", "2.4.1", "https://api.nuget.org/v3-flatcontainer/xunit.1/2.4.1/xunit.1.2.4.1.nupkg")])
def test_from_request_url(expected_package_id, expected_package_version, request_url):
    found =  PackageDefinition.from_request_url(request_url)
    assert found and found[0] == PackageDefinition(expected_package_id, expected_package_version)

# To invoke the pytest framework and run all tests
if __name__ == "__main__":
    pytest.main()
