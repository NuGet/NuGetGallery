from __future__ import annotations
from dataclasses import dataclass, field
from collections import namedtuple
from typing import List, Optional, Tuple
from functools import total_ordering

Version = namedtuple("Version", ["major", "minor", "patch", "revision"])

@dataclass
@total_ordering
class SemanticVersion:
    major: int
    minor: int
    patch: int
    revision: int
    release_labels: Optional[List[str]] = field(default_factory=list)
    metadata: Optional[str] = None
    version: Optional[Version] = field(init=False)

    def __post_init__(self):
        self.version = (self.major, self.minor, self.patch, self.revision)

    def to_normalized_string(self) -> str:
        return self.__str__()

    def to_full_string(self) -> str:
        result = f"{self.major}.{self.minor}.{self.patch}"
        if(self.revision > 0):
            result += f".{self.revision}"
        if self.release_labels:
            result += "-" + ".".join(self.release_labels)
        if self.metadata:
            result += "+" + self.metadata
        return result

    def __str__(self):
        return self.to_full_string()

    def __eq__(self, other):
        if not isinstance(other, SemanticVersion):
            return NotImplemented
        return (self.version, self.release_labels, self.metadata) == (other.version, other.release_labels, other.metadata)

    def __lt__(self, other):
        if not isinstance(other, SemanticVersion):
            return NotImplemented
        return (self.version, self.release_labels, self.metadata) < (other.version, other.release_labels, other.metadata)

    def __hash__(self):
        return hash((self.version, tuple(self.release_labels), self.metadata))

    @classmethod
    def parse(cls, value: str) -> SemanticVersion:
        try:
            version_string, release_labels, build_metadata = cls.parse_sections(value)
            major, minor, patch = cls.parse_version(version_string)
            return cls(major, minor, patch, release_labels, build_metadata)
        except Exception as e:
            raise ValueError(f"Invalid SemanticVersion string: {value}") from e

    @classmethod
    def try_parse(cls, value: str) -> Optional[SemanticVersion]:
        try:
            return cls.parse(value)
        except ValueError:
            return None

    @staticmethod
    def parse_sections(value: str) -> Tuple[str, Optional[List[str]], Optional[str]]:
        if "+" in value:
            value, build_metadata = value.split("+", 1)
        else:
            build_metadata = None

        if "-" in value:
            value, release_labels = value.split("-", 1)
            release_labels = release_labels.split(".")
        else:
            release_labels = None

        return value, release_labels, build_metadata

    @staticmethod
    def parse_version(version_string: str) -> Tuple[int, int, int]:
        parts = version_string.split(".")
        if len(parts) != 3:
            raise ValueError(f"Invalid version string: {version_string}")
        return int(parts[0]), int(parts[1]), int(parts[2])
