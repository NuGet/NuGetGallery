import pytest
from unittest import mock
import builtins

from loginterpretation.packagedefinition import PackageDefinition

def test_url():
    found =  PackageDefinition.from_request_url("https://api.nuget.org/v3-flatcontainer/xunit.1/2.4.1/xunit.1.2.4.1.nupkg")
    assert found == ""

# To invoke the pytest framework and run all tests
if __name__ == "__main__":
    pytest.main()
