import urllib.parse


class PackageDefinition:

    NUGET_EXTENSION = ".nupkg"
    VERSION_QUERY_PARAMETER = "packageVersion"

    def __init__(self, package_id=None, package_version=None):
        self.package_id = package_id.strip() if package_id else None
        self.package_version = package_version.strip() if package_version else None

    @staticmethod
    def from_request_url(request_url):
        if not request_url or not request_url.lower().endswith(
            PackageDefinition.NUGET_EXTENSION
        ):
            return None

        resolution_options = []

        parsed = urllib.parse.urlparse(request_url)
        url_segments = [segment for segment in parsed.path.split("/") if segment]

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
            version_param = urllib.parse.parse_qs(parsed.query)[
                PackageDefinition.VERSION_QUERY_PARAMETER
            ]
            if version_param:
                # remove it from the file name and use that as the package id

                parsed_id = file_name[: -len(version_param)]
                # sanity check to make sure the version param matches what's in the file name
                if file_name.lower() == f"{parsed_id}.{version_param}".lower():
                    resolution_options.append(
                        PackageDefinition(parsed_id, version_param)
                    )

        return resolution_options
