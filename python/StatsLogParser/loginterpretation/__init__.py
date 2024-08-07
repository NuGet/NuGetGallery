"""
Log interpretation package
"""
import os
import sys
import pkg_resources

clients_yaml = pkg_resources.resource_filename(__name__, "knownclients.yaml")
os.environ["UA_PARSER_YAML"] =  clients_yaml
