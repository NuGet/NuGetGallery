from __future__ import annotations
from dataclasses import dataclass
from typing import List, Optional, Tuple
from .semanticversion import SemanticVersion, Version

@dataclass
class NuGetVersion(SemanticVersion):

    @classmethod
    def parse(cls, value: str) -> NuGetVersion:
        try:
            version_string, release_labels, build_metadata = cls.parse_sections(value)
            major, minor, patch, revision = cls.parse_version(version_string)
            return cls(major, minor, patch, revision, release_labels, build_metadata)
        except Exception as e:
            raise ValueError(f"Invalid NuGetVersion string: {value}") from e

    @classmethod
    def try_parse(cls, value: str) -> Optional[NuGetVersion]:
        try:
            return cls.parse(value)
        except ValueError:
            return None

    @staticmethod
    def parse_version(version_string: str) -> Version:
        parts = version_string.split(".")
        if len(parts) == 3:
            return int(parts[0]), int(parts[1]), int(parts[2]), 0
        elif len(parts) == 4:
            return int(parts[0]), int(parts[1]), int(parts[2]), int(parts[3])
        else:
            raise ValueError(f"Invalid version string: {version_string}")

    @staticmethod
    def is_letter_or_digit_or_dash(c: str) -> bool:
        return c.isalnum() or c == '-'

    @staticmethod
    def is_digit(c: str) -> bool:
        return c.isdigit()

    @staticmethod
    def is_valid(s: str, allow_leading_zeros: bool) -> bool:
        if not allow_leading_zeros and s.startswith("0") and len(s) > 1:
            return False
        return all(NuGetVersion.is_letter_or_digit_or_dash(c) for c in s)

    @staticmethod
    def is_valid_part(s: str, allow_leading_zeros: bool) -> bool:
        return NuGetVersion.is_valid(s, allow_leading_zeros)

    @staticmethod
    def normalize_version_value(version: Version) -> Version:
        return version
