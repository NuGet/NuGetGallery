from __future__ import annotations
from collections import namedtuple
from typing import Optional
import re
import pkgutil
from ua_parser import user_agent_parser
from ua_parser._regexes import USER_AGENT_PARSERS
import yaml

UserAgent = namedtuple('UserAgent', ['family', 'major', 'minor', 'patch'])

class UserAgentParser:
    """UserAgentParser class to parse user agent string."""
    DEFAULT_PARSER_DATA = USER_AGENT_PARSERS
    KNOWN_CLIENTS_DATA: list[user_agent_parser.UserAgentParser] = []
    KNOWN_CLIENTS_IN_CHINA_DATA: list[user_agent_parser.UserAgentParser] = []

    @classmethod
    def __static_init__(cls):
        cls.KNOWN_CLIENTS_DATA = cls._load_known_clients_parser()
        cls.KNOWN_CLIENTS_IN_CHINA_DATA = cls._load_known_clients_in_china_parser()

    @staticmethod
    def _load_known_clients_parser():
        yaml_content = UserAgentParser._read_known_clients_yaml()
        return UserAgentParser._create_parser_data_from_yaml(yaml_content)

    @staticmethod
    def _load_known_clients_in_china_parser():
        yaml_content = UserAgentParser._read_known_clients_yaml()
        patched_yaml = UserAgentParser._add_support_for_china_cdn(yaml_content)
        return UserAgentParser._create_parser_data_from_yaml(patched_yaml)

    @staticmethod
    def _add_support_for_china_cdn(yaml_content):
        patched_yaml = re.sub(
            r"(?:[:]\s'\()+([\w\-.\s]+)(?:\))+",
            UserAgentParser._replace_whitespace_with_plus_sign,
            yaml_content,
            flags=re.DOTALL
        )
        return patched_yaml

    @staticmethod
    def _replace_whitespace_with_plus_sign(match):
        return ": '(" + match.group(1).replace(" ", r"\+") + ")"

    @staticmethod
    def _read_known_clients_yaml() -> str:
        file_data = pkgutil.get_data(__name__, 'knownclients.yaml').decode('utf-8-sig')
        return file_data

    @staticmethod
    def _create_parser_data_from_yaml(yaml_content) -> list[user_agent_parser.UserAgentParser]:
        data = yaml.safe_load(yaml_content)

        parsers: list[user_agent_parser.UserAgentParser] = []

        for parser in data["user_agent_parsers"]:
            regex = parser["regex"]

            family_replacement = parser.get("family_replacement")
            v1_replacement = parser.get("v1_replacement")
            v2_replacement = parser.get("v2_replacement")

            parsers.append(
                user_agent_parser.UserAgentParser(
                    regex, family_replacement, v1_replacement, v2_replacement
                )
            )

        return parsers

    _MAX_CACHE_SIZE = 200
    _PARSE_CACHE: dict[str, UserAgent] = {}

    @staticmethod
    def _lookup(ua: str) -> Optional[UserAgent]:

        entry = UserAgentParser._PARSE_CACHE.get(ua)
        if entry is not None:
            return entry

        if len(UserAgentParser._PARSE_CACHE) >= UserAgentParser._MAX_CACHE_SIZE:
            UserAgentParser._PARSE_CACHE.clear()

        return None

    @staticmethod
    def parse(user_agent_string):
        """Parse using known clients parser, then known clients in China parser, then default parser."""
        entry = UserAgentParser._lookup(user_agent_string)

        if entry is not None:
            return entry

        # Try known clients parser
        entry = UserAgentParser._parse_user_agent_with_parsers(user_agent_string, UserAgentParser.KNOWN_CLIENTS_DATA)

        if entry.family.lower() == 'other': # Try China parser
            entry = UserAgentParser._parse_user_agent_with_parsers(user_agent_string, UserAgentParser.KNOWN_CLIENTS_IN_CHINA_DATA)

        if entry.family.lower() == 'other': # Try default parser
            entry = UserAgentParser._parse_user_agent_with_parsers(user_agent_string, UserAgentParser.DEFAULT_PARSER_DATA)

        UserAgentParser._PARSE_CACHE[user_agent_string] = entry
        return entry

    @staticmethod
    def _parse_user_agent_with_parsers(user_agent_string: str, parsers: list[user_agent_parser.UserAgentParser]) -> UserAgent:
        for ua_parser in parsers:
            family, v1, v2, v3 = ua_parser.Parse(user_agent_string)
            if family:
                break

        family = family or "Other"
        return UserAgent(family, v1 or None, v2 or None, v3 or None)

UserAgentParser.__static_init__()
