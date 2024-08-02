from __future__ import annotations
from dataclasses import dataclass
from packaging.version import Version, InvalidVersion
import urllib.parse

@dataclass(init=False)
class PackageDefinition:
    """dataclass to represent a package definition"""

    NUGET_EXTENSION = ".nupkg"
    VERSION_QUERY_PARAMETER = "packageVersion"
    package_id: str
    package_version: str

    def __init__(self, package_id: str = None, package_version: str = None) -> None:
        self.package_id = package_id.strip() if package_id else None
        self.package_version = package_version.strip() if package_version else None

    @staticmethod
    def from_request_url(request_url) -> list[PackageDefinition]:
        """Static method to create a PackageDefinition from a request url"""

        if not request_url:
            return None

        resolution_options = []

        parsed = urllib.parse.urlparse(request_url)
        if(not parsed.path.lower().endswith(PackageDefinition.NUGET_EXTENSION)):
            return None

        url_segments = [segment for segment in urllib.parse.unquote(parsed.path).split("/") if segment]

        file_name = url_segments[-1]
        file_name = file_name[: -len(PackageDefinition.NUGET_EXTENSION)]

        # Special handling for flat container
        if len(url_segments) > 3:
            package_id_container = url_segments[-3]
            package_version_container = url_segments[-2]

            if (
                file_name.lower()
                == f"{package_id_container}.{package_version_container}".lower()
            ):
                resolution_options.append(
                    PackageDefinition(package_id_container, package_version_container)
                )

        # Look for it in the query string
        if not resolution_options:
            query_params = urllib.parse.parse_qs(parsed.query)
            version_param_list = query_params.get(PackageDefinition.VERSION_QUERY_PARAMETER, None)
            if version_param_list is not None:
                version_param = version_param_list[0]
                # remove it from the file name and use that as the package id

                parsed_id = file_name[: -(len(version_param) + 1)]
                # sanity check to make sure the version param matches what's in the file name
                if file_name.lower() == f"{parsed_id}.{version_param}".lower():
                    resolution_options.append(
                        PackageDefinition(parsed_id, version_param)
                    )
            else:
                # Look for it in the file name but this can be ambiguous if the package id ends in a number
                next_dot_index = file_name.find('.')

                while next_dot_index != -1:
                    package_part = file_name[:next_dot_index]
                    version_part = file_name[next_dot_index + 1:]

                    if PackageDefinition.is_valid_version(version_part):
                        resolution_options.append(PackageDefinition(package_part, version_part))

                    next_dot_index = file_name.find('.', next_dot_index + 1)

        return resolution_options

    @staticmethod
    def is_valid_version(version_string):
        """Check if it's a valid version that support 3 or 4 part versioning with optional pre-release tag"""
        try:
            # Check if it's a valid three-part or four-part version with optional pre-release tag
            Version(version_string)
            return True
        except InvalidVersion:
            # Check if it's a valid four-part version with optional pre-release tag
            parts = version_string.split('.')
            if len(parts) == 4:
                main_version = '.'.join(parts[:3])
                pre_release = parts[3]
                try:
                    Version(main_version)
                    if pre_release.isdigit() or '-' in pre_release:
                        return True
                except InvalidVersion:
                    pass
        return False
