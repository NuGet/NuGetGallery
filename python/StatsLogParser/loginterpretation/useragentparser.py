import pkgutil
import re
from ua_parser import user_agent_parser
import logging

class UserAgentParser:
    def __init__(self):
        self._default_parser = user_agent_parser.Parse
        self._known_clients_parser = self._load_known_clients_parser()
        self._known_clients_in_china_parser = self._load_known_clients_in_china_parser()

    def _load_known_clients_parser(self):
        yaml_content = self._read_known_clients_yaml()
        return self._create_parser_from_yaml(yaml_content)

    def _load_known_clients_in_china_parser(self):
        yaml_content = self._read_known_clients_yaml()
        patched_yaml = self._add_support_for_china_cdn(yaml_content)
        return self._create_parser_from_yaml(patched_yaml)

    def _add_support_for_china_cdn(self, yaml_content):
        patched_yaml = re.sub(
            r"(?:[:]\s'\()+([\w-.\s]+)(?:\))+",
            self._replace_whitespace_with_plus_sign,
            yaml_content,
            flags=re.DOTALL
        )
        return patched_yaml

    def _replace_whitespace_with_plus_sign(self, match):
        return ": '(" + match.group(1).replace(" ", r"\+") + ")"

    def _read_known_clients_yaml(self) -> str:
        try:
            #data = pkgutil. (__name__, 'knownclients.yaml')
            return ""
        except Exception as e:
            logging.error(f"Failed to read known clients YAML: {str(e)}")
            return ""

    def _create_parser_from_yaml(self, yaml_content):
        return user_agent_parser.Parse

    def parse_user_agent(self, user_agent):
        parsed_result = self._parse_with_known_clients_parser(user_agent, self._known_clients_parser)
        if parsed_result['user_agent']['family'].lower() == 'other':
            parsed_result = self._parse_with_known_clients_parser(user_agent, self._known_clients_in_china_parser)
        if parsed_result['user_agent']['family'].lower() == 'other':
            parsed_result = self._default_parser(user_agent)
        return parsed_result

    def parse_os(self, user_agent):
        parsed_result = self._parse_with_parser(user_agent, self._known_clients_parser, user_agent_parser.ParseOS)
        if parsed_result['os']['family'].lower() == 'other':
            parsed_result = self._parse_with_parser(user_agent, self._known_clients_in_china_parser, user_agent_parser.ParseOS)
        if parsed_result['os']['family'].lower() == 'other':
            parsed_result = user_agent_parser.ParseOS(user_agent)
        return parsed_result

    def parse_device(self, user_agent):
        parsed_result = self._parse_with_parser(user_agent, self._known_clients_parser, user_agent_parser.ParseDevice)
        if parsed_result['device']['family'].lower() == 'other':
            parsed_result = self._parse_with_parser(user_agent, self._known_clients_in_china_parser, user_agent_parser.ParseDevice)
        if parsed_result['device']['family'].lower() == 'other':
            parsed_result = user_agent_parser.ParseDevice(user_agent)
        return parsed_result

    def _parse_with_known_clients_parser(self, user_agent, parser):
        try:
            return parser(user_agent)
        except Exception as e:
            logging.error(f"Failed to parse with known clients parser: {str(e)}")
            return {'user_agent': {'family': 'other'}}

    def _parse_with_parser(self, user_agent, parser, parse_function):
        try:
            return parse_function(user_agent)
        except Exception as e:
            logging.error(f"Failed to parse with parser: {str(e)}")
            return {'os': {'family': 'other'}, 'device': {'family': 'other'}}
