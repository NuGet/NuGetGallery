// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.TestData
{
    internal static class McpServerData
    {
        public const string McpJsonMinimal =
            """
            {
              "inputs": [],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes"],
                  "env": {}
                }
              }
            }
            """;

        public const string ServerJsonValid =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version_detail": {
                "version": "3.1.0",
                "release_date": "2024-03-05T16:45:00Z"
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "runtime_arguments": [
                    {
                      "type": "named",
                      "name": "--network",
                      "value": "host",
                      "description": "Use host network mode"
                    },
                    {
                      "type": "named",
                      "name": "-e",
                      "value": "DB_TYPE={db_type}",
                      "description": "Database type to connect to",
                      "is_repeated": true,
                      "variables": {
                        "db_type": {
                          "description": "Type of database",
                          "choices": ["postgres", "mysql", "mongodb", "redis"],
                          "is_required": true
                        }
                      }
                    },
                    {
                      "type": "positional",
                      "value_hint": "database_size",
                      "description": "Size of the database to connect to",
                      "value": "{db_size}",
                      "variables": {
                        "db_size": {
                          "description": "Database size",
                          "is_required": true
                        }
                      }
                    }
                  ],
                  "package_arguments": [
                    {
                      "type": "named",
                      "name": "--host",
                      "description": "Database host",
                      "value": "localhost",
                      "is_required": true
                    },
                    {
                      "type": "named",
                      "name": "--port",
                      "description": "Database port",
                      "format": "number",
                      "value": "{db_port}",
                      "variables": {
                        "db_port": {
                          "description": "Database port",
                          "is_required": true
                        }
                      }
                    },
                    {
                      "type": "positional",
                      "value_hint": "database_name",
                      "description": "Name of the database to connect to",
                      "value": "{db_name}",
                      "variables": {
                        "db_name": {
                          "description": "Database name",
                          "is_required": true
                        }
                      }
                    }
                  ],
                  "environment_variables": [
                    {
                      "name": "DB_USERNAME",
                      "description": "Database username",
                      "value": "DB_USERNAME={db_username}",
                      "variables": {
                        "db_username": {
                          "description": "Database username",
                          "is_required": true
                        }
                      }
                    },
                    {
                      "name": "DB_PASSWORD",
                      "description": "Database password",
                      "value": "DB_PASSWORD={db_password}",
                      "variables": {
                        "db_password": {
                          "description": "Database password",
                          "is_required": true,
                          "is_secret": true
                        }
                      }
                    },
                    {
                      "name": "SSL_MODE",
                      "description": "SSL connection mode",
                      "value": "SSL_MODE={ssl_mode}",
                      "variables": {
                        "ssl_mode": {
                          "description": "SSL connection mode",
                          "default": "prefer",
                          "choices": ["disable", "prefer", "require"]
                        }
                      }
                    }
                  ]
                }
              ]
            }
            """;

        public const string ServerJsonValidMinified =
            """
            {"name":"my-db-mcp","description":"my db mcp","repository":{"url":"https://github.com/example/my-db-mcp","source":"github","id":"ghi789jk-lmno-1234-pqrs-tuvwxyz56789"},"version_detail":{"version":"3.1.0","release_date":"2024-03-05T16:45:00Z"},"packages":[{"registry_name":"nuget","name":"example/my-db-mcp","version":"3.1.0","runtime_arguments":[{"type":"named","name":"--network","value":"host","description":"Use host network mode"},{"type":"named","name":"-e","value":"DB_TYPE={db_type}","description":"Database type to connect to","is_repeated":true,"variables":{"db_type":{"description":"Type of database","choices":["postgres","mysql","mongodb","redis"],"is_required":true}}},{"type":"positional","value_hint":"database_size","description":"Size of the database to connect to","value":"{db_size}","variables":{"db_size":{"description":"Database size","is_required":true}}}],"package_arguments":[{"type":"named","name":"--host","description":"Database host","value":"localhost","is_required":true},{"type":"named","name":"--port","description":"Database port","format":"number","value":"{db_port}","variables":{"db_port":{"description":"Database port","is_required":true}}},{"type":"positional","value_hint":"database_name","description":"Name of the database to connect to","value":"{db_name}","variables":{"db_name":{"description":"Database name","is_required":true}}}],"environment_variables":[{"name":"DB_USERNAME","description":"Database username","value":"DB_USERNAME={db_username}","variables":{"db_username":{"description":"Database username","is_required":true}}},{"name":"DB_PASSWORD","description":"Database password","value":"DB_PASSWORD={db_password}","variables":{"db_password":{"description":"Database password","is_required":true,"is_secret":true}}},{"name":"SSL_MODE","description":"SSL connection mode","value":"SSL_MODE={ssl_mode}","variables":{"ssl_mode":{"description":"SSL connection mode","default":"prefer","choices":["disable","prefer","require"]}}}]}]}
            """;

        public const string McpJsonValid =
            """
            {
              "inputs": [
                {
                  "type": "pickString",
                  "id": "db_type",
                  "description": "Type of database",
                  "options": ["postgres", "mysql", "mongodb", "redis"]
                },
                {
                  "type": "promptString",
                  "id": "db_size",
                  "description": "Database size"
                },
                {
                  "type": "promptString",
                  "id": "db_username",
                  "description": "Database username"
                },
                {
                  "type": "promptString",
                  "id": "db_password",
                  "description": "Database password",
                  "password": true
                },
                {
                  "type": "pickString",
                  "id": "ssl_mode",
                  "description": "SSL connection mode",
                  "default": "prefer",
                  "options": ["disable", "prefer", "require"]
                },
                {
                  "type": "promptString",
                  "id": "db_port",
                  "description": "Database port"
                },
                {
                  "type": "promptString",
                  "id": "db_name",
                  "description": "Database name"
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["--network", "host", "-e", "DB_TYPE={input:db_type}", "{input:db_size}", "Test.McpServer@1.0.0", "--yes", "--", "--host", "localhost", "--port", "${input:db_port}", "${input:db_name}"],
                  "env": {
                    "DB_USERNAME": "DB_USERNAME=${input:db_username}",
                    "DB_PASSWORD": "DB_PASSWORD=${input:db_password}",
                    "SSL_MODE": "SSL_MODE=${input:ssl_mode}"
                  }
                }
              }
            }
            """;

        public const string ServerJsonNoPackages =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version_detail": {
                "version": "3.1.0",
                "release_date": "2024-03-05T16:45:00Z"
              },
              "packages": []
            }
            """;

        public const string ServerJsonNoNugetRegistry =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version_detail": {
                "version": "3.1.0",
                "release_date": "2024-03-05T16:45:00Z"
              },
              "packages": [
                {
                  "registry_name": "npm",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "package_arguments": [],
                  "environment_variables": []
                }
              ]
            }
            """;

        public const string ServerJsonEmptyArgsAndEnv =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version_detail": {
                "version": "3.1.0",
                "release_date": "2024-03-05T16:45:00Z"
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "package_arguments": [],
                  "environment_variables": []
                }
              ]
            }
            """;

        public const string ServerJsonNoArgsAndEnv =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version_detail": {
                "version": "3.1.0",
                "release_date": "2024-03-05T16:45:00Z"
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0"
                }
              ]
            }
            """;

        public const string ServerJsonNoNamedArgValues =
            """
            {
              "name": "",
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "",
                  "version": "",
                  "package_arguments": [
                    {
                      "type": "named",
                      "name": "",
                      "description": "",
                      "value": "",
                      "is_repeated": null,
                      "format": "",
                      "choices": [],
                      "is_required": null
                    }
                  ]
                }
              ]
            }
            """;

        public const string ServerJsonNoPositionalArgValues =
            """
            {
              "name": "",
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "",
                  "version": "",
                  "package_arguments": [
                    {
                      "type": "positional",
                      "value_hint": "",
                      "description": "",
                      "value": "",
                      "default": "",
                      "is_repeated": null,
                      "format": "",
                      "choices": [],
                      "is_required": null
                    }
                  ]
                }
              ]
            }
            """;

        public const string ServerJsonNoEnvVarValues =
            """
            {
              "name": "",
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "",
                  "version": "",
                  "environment_variables": [
                    {
                      "name": "",
                      "description": "",
                      "default": "",
                      "is_required": null,
                      "is_secret": null,
                      "choices": []
                    }
                  ]
                }
              ]
            }
            """;

        public const string ServerJsonNullString =
            """
            {
              "name": null,
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              }
            }
            """;

        public const string ServerJsonNullList =
            """
            {
              "name": "",
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              },
              "packages": null
            }
            """;

        public const string ServerJsonNullPackage =
            """
            {
              "name": "",
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              },
              "packages": [null]
            }
            """;

        public const string ServerJsonNullPackageArgument =
            """
            {
              "name": "",
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "",
                  "version": "",
                  "package_arguments": [null]
                }
              ]
            }
            """;

        public const string ServerJsonNullEnvVar =
            """
            {
              "name": "",
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "",
                  "version": "",
                  "environment_variables": [null]
                }
              ]
            }
            """;

        public const string ServerJsonNullVariables =
            """
            {
              "name": "",
              "description": "",
              "repository": {
                "url": "",
                "source": "",
                "id": ""
              },
              "version_detail": {
                "version": "",
                "release_date": ""
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "",
                  "version": "",
                   "package_arguments": [
                    {
                      "type": "named",
                      "name": "--port",
                      "description": "Database port",
                      "format": "number",
                      "value": "{db_port}",
                      "variables": {
                        "db_port": null
                      }
                    },
                  ]
                }
              ]
            }
            """;

        public const string ServerJsonNonTypedPackageArgs =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version_detail": {
                "version": "3.1.0",
                "release_date": "2024-03-05T16:45:00Z"
              },
              "packages": [
                {
                  "registry_name": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "package_arguments": [
                    {
                      "type": "",
                      "name": "",
                      "description": "",
                      "value": "",
                      "is_repeated": null,
                      "format": "",
                      "choices": [],
                      "is_required": null
                    },
                    {
                      "type": "",
                      "value_hint": "",
                      "description": "",
                      "value": "",
                      "default": "",
                      "is_repeated": null,
                      "format": "",
                      "choices": [],
                      "is_required": null
                    },
                    {
                      "type": "",
                      "description": "",
                      "value": "",
                      "is_required": null,
                      "is_repeated": null,
                      "format": "",
                      "choices": []
                    }
                  ]
                }
              ]
            }
            """;

        public const string ServerJsonEnvVarNameButNoValue =
            """
            {
              "packages": [
                {
                  "registry_name": "nuget",
                  "environment_variables": [
                    {
                      "name": "Foo",
                      "description": "",
                    }
                  ]
                }
              ]
            }
            """;

        public const string McpJsonEnvVarNameButNoValue =
            """
            {
              "inputs": [],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes"],
                  "env": {
                    "Foo": ""
                  }
                }
              }
            }
            """;
    }
}
