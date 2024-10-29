import sys
sys.path.append('..') # This is to add the parent directory to the path so that the module can be imported
from loginterpretation.clientnametranslation import ClientNameTranslation
import pytest

@pytest.mark.parametrize("expected_category,client_name", [
    ("NuGet", "NuGet MSBuild Task"),
    ("NuGet", "NuGet Command Line"),
    ("NuGet", "NuGet Client V3"),
    ("Browser", "safari"),
    ("Browser", "chrome"),
    ("Crawler", "bot"),
    ("Crawler", "spider"),
    ("Crawler", "slurp"),
    ("Script", "Powershell"),
    ("Script", "PowerShell"),
    ("NuGet Package Explorer", "NuGet Package Explorer")])
def test_clientname_returns_correct_category(expected_category, client_name):
    found =  ClientNameTranslation.get_client_category(client_name)
    assert found and found == expected_category

# To invoke the pytest framework and run all tests
if __name__ == "__main__":
    pytest.main()
