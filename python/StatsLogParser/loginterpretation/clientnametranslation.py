from dataclasses import dataclass, field
from typing import List

# Client Categories using dataclass
@dataclass(frozen=True)
class ClientCategories:
    nuget: str = "NuGet"
    webmatrix: str = "WebMatrix"
    nuget_package_explorer: str = "NuGet Package Explorer"
    script: str = "Script"
    crawler: str = "Crawler"
    mobile: str = "Mobile"
    browser: str = "Browser"
    unknown: str = "Unknown"

# Client Names using dataclass
@dataclass(frozen=True)
class ClientNames:
    nuget: List[str] = field(default_factory=lambda: [
        "NuGet Cross-Platform Command Line",
        "NuGet Client V3",
        "NuGet VS VSIX",
        "NuGet VS PowerShell Console",
        "NuGet VS Packages Dialog - Solution",
        "NuGet VS Packages Dialog",
        "NuGet Shim",
        "NuGet Add Package Dialog",
        "NuGet Command Line",
        "NuGet Package Manager Console",
        "NuGet Visual Studio Extension",
        "Package-Installer",
        "NuGet MSBuild Task",
        "NuGet .NET Core MSBuild Task",
        "NuGet Desktop MSBuild Task"
    ])

    webmatrix: List[str] = field(default_factory=lambda: ["WebMatrix"])

    nuget_package_explorer: List[str] = field(default_factory=lambda: [
        "NuGet Package Explorer Metro",
        "NuGet Package Explorer"
    ])

    script: List[str] = field(default_factory=lambda: [
        "Powershell",
        "curl",
        "Wget",
        "Java"
    ])

    crawler: List[str] = field(default_factory=lambda: [
        "Bot",
        "bot",
        "Slurp",
        "BingPreview",
        "crawler",
        "sniffer",
        "spider"
    ])

    mobile: List[str] = field(default_factory=lambda: [
        "Mobile",
        "Android",
        "Kindle",
        "BlackBerry",
        "Openwave",
        "NetFront",
        "CFNetwork",
        "iLunascape"
    ])

    browser: List[str] = field(default_factory=lambda: [
        "Mozilla",
        "Firefox",
        "Opera",
        "Chrome",
        "Chromium",
        "Internet Explorer",
        "Browser",
        "Safari",
        "Sogou Explorer",
        "Maxthon",
        "SeaMonkey",
        "Iceweasel",
        "Sleipnir",
        "Konqueror",
        "Lynx",
        "Galeon",
        "Epiphany",
        "Lunascape"
    ])

    absolute_browser_names: List[str] = field(default_factory=lambda: [
        "IE",
        "Iron"
    ])

    unknown: List[str] = field(default_factory=lambda: [
        "PhantomJS",
        "WebKit Nightly",
        "Python Requests",
        "Jasmine",
        "Java",
        "AppleMail",
        "NuGet Test Client"
    ])

# Client Name Translation Logic using static methods
class ClientNameTranslation:
    @staticmethod
    def get_client_category(client_name: str) -> str:
        if not client_name or client_name.strip() == "":
            return ""

        if ClientNameTranslation.contains_any_client_name(client_name, ClientNames().nuget):
            return ClientCategories().nuget

        if ClientNameTranslation.contains_any_client_name(client_name, ClientNames().webmatrix):
            return ClientCategories().webmatrix

        if ClientNameTranslation.contains_any_client_name(client_name, ClientNames().nuget_package_explorer):
            return ClientCategories().nuget_package_explorer

        if ClientNameTranslation.contains_any_client_name(client_name, ClientNames().script):
            return ClientCategories().script

        if ClientNameTranslation.contains_any_client_name(client_name, ClientNames().crawler):
            return ClientCategories().crawler

        if ClientNameTranslation.contains_any_client_name(client_name, ClientNames().mobile):
            return ClientCategories().mobile

        # Check these late in the process, because other User Agents tend to also send browser strings
        if (ClientNameTranslation.contains_any_client_name(client_name, ClientNames().browser) or
            any(client_name.strip().lower() == abn.lower() for abn in ClientNames().absolute_browser_names)):
            return ClientCategories().browser

        # Explicitly categorize unknowns, test frameworks, or others that should be filtered out in the reports
        if ClientNameTranslation.contains_any_client_name(client_name, ClientNames().unknown):
            return ClientCategories().unknown

        # Return empty for all others to allow ecosystem user agents to be picked up in the reports
        return ""

    @staticmethod
    def contains(source: str, target: str, comparison=str.casefold) -> bool:
        return comparison(target) in comparison(source)

    @staticmethod
    def contains_any_client_name(source: str, target_list: List[str], comparison=str.casefold) -> bool:
        if not source or source.strip() == "":
            return False
        return any(ClientNameTranslation.contains(source, target, comparison) for target in target_list)
