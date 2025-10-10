// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.TestData
{
    internal static class McpServerData
    {
        public const string McpJsonMinimal =
            """
            {
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes"]
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "runtimeArguments": [
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
                      "isRepeated": true,
                      "variables": {
                        "db_type": {
                          "description": "Type of database",
                          "choices": ["postgres", "mysql", "mongodb", "redis"],
                          "isRequired": true
                        }
                      }
                    },
                    {
                      "type": "positional",
                      "valueHint": "database_size",
                      "description": "Size of the database to connect to",
                      "value": "{db_size}",
                      "variables": {
                        "db_size": {
                          "description": "Database size",
                          "isRequired": true
                        }
                      }
                    }
                  ],
                  "packageArguments": [
                    {
                      "type": "named",
                      "name": "--host",
                      "description": "Database host",
                      "value": "localhost",
                      "isRequired": true
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
                          "isRequired": true
                        }
                      }
                    },
                    {
                      "type": "positional",
                      "valueHint": "database_name",
                      "description": "Name of the database to connect to",
                      "value": "{db_name}",
                      "variables": {
                        "db_name": {
                          "description": "Database name",
                          "isRequired": true
                        }
                      }
                    }
                  ],
                  "environmentVariables": [
                    {
                      "name": "DB_USERNAME",
                      "description": "Database username",
                      "value": "{db_username}",
                      "variables": {
                        "db_username": {
                          "description": "Database username",
                          "isRequired": true
                        }
                      }
                    },
                    {
                      "name": "DB_PASSWORD",
                      "description": "Database password",
                      "value": "{db_password}",
                      "variables": {
                        "db_password": {
                          "description": "Database password",
                          "isRequired": true,
                          "isSecret": true
                        }
                      }
                    },
                    {
                      "name": "SSL_MODE",
                      "description": "SSL connection mode",
                      "value": "{ssl_mode}",
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
            {"name":"my-db-mcp","description":"my db mcp","repository":{"url":"https://github.com/example/my-db-mcp","source":"github","id":"ghi789jk-lmno-1234-pqrs-tuvwxyz56789"},"version":"1.0.0","packages":[{"registryType":"nuget","name":"example/my-db-mcp","version":"3.1.0","runtimeArguments":[{"type":"named","name":"--network","value":"host","description":"Use host network mode"},{"type":"named","name":"-e","value":"DB_TYPE={db_type}","description":"Database type to connect to","isRepeated":true,"variables":{"db_type":{"description":"Type of database","choices":["postgres","mysql","mongodb","redis"],"isRequired":true}}},{"type":"positional","valueHint":"database_size","description":"Size of the database to connect to","value":"{db_size}","variables":{"db_size":{"description":"Database size","isRequired":true}}}],"packageArguments":[{"type":"named","name":"--host","description":"Database host","value":"localhost","isRequired":true},{"type":"named","name":"--port","description":"Database port","format":"number","value":"{db_port}","variables":{"db_port":{"description":"Database port","isRequired":true}}},{"type":"positional","valueHint":"database_name","description":"Name of the database to connect to","value":"{db_name}","variables":{"db_name":{"description":"Database name","isRequired":true}}}],"environmentVariables":[{"name":"DB_USERNAME","description":"Database username","value":"{db_username}","variables":{"db_username":{"description":"Database username","isRequired":true}}},{"name":"DB_PASSWORD","description":"Database password","value":"{db_password}","variables":{"db_password":{"description":"Database password","isRequired":true,"isSecret":true}}},{"name":"SSL_MODE","description":"SSL connection mode","value":"{ssl_mode}","variables":{"ssl_mode":{"description":"SSL connection mode","default":"prefer","choices":["disable","prefer","require"]}}}]}]}
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
                  "args": ["--network", "host", "-e", "DB_TYPE=${input:db_type}", "${input:db_size}", "Test.McpServer@1.0.0", "--yes", "--", "--host", "localhost", "--port", "${input:db_port}", "${input:db_name}"],
                  "env": {
                    "DB_USERNAME": "${input:db_username}",
                    "DB_PASSWORD": "${input:db_password}",
                    "SSL_MODE": "${input:ssl_mode}"
                  }
                }
              }
            }
            """;

        public const string ServerJsonPositionalWithValue =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                        "type": "positional",
                        "value": "mcp"
                    },
                    {
                        "type": "positional",
                        "value": "start"
                    }
                  ],
                }
              ]
            }
            """;

        public const string McpJsonPositionalWithValue =
            """
            {
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "mcp", "start"]
                }
              }
            }
            """;

        public const string ServerJsonPositionalWithValueHint =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "positional",
                      "valueHint": "target_dir"
                    }
                  ],
                }
              ]
            }
            """;

        public const string McpJsonPositionalWithValueHint =
            """
            {
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "target_dir"]
                }
              }
            }
            """;

        public const string ServerJsonPositionalWithValueHintDescription =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "positional",
                      "valueHint": "target_dir",
                      "description": "Path to access"
                    }
                  ],
                }
              ]
            }
            """;

        public const string McpJsonPositionalWithValueHintDescription =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "target_dir",
                  "description": "Path to access"
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "${input:target_dir}"]
                }
              }
            }
            """;

        public const string ServerJsonPositionalWithValueHintChoices =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "positional",
                      "valueHint": "database_name",
                      "choices": ["foo", "bar"]
                    }
                  ],
                }
              ]
            }
            """;

        public const string McpJsonPositionalWithValueHintChoices =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "database_name",
                  "description": "",
                  "options": ["foo", "bar"]
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "${input:database_name}"]
                }
              }
            }
            """;

        public const string ServerJsonPositionalWithValueHintDefault =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "positional",
                      "valueHint": "target_dir",
                      "default": "/Users/username/Desktop"
                    }
                  ],
                }
              ]
            }
            """;

        public const string McpJsonPositionalWithValueHintDefault =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "target_dir",
                  "description": "",
                  "default": "/Users/username/Desktop"
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "${input:target_dir}"]
                }
              }
            }
            """;

        public const string ServerJsonNamedWithValue =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "named",
                      "name": "--host",
                      "value": "localhost"
                    },
                  ],
                }
              ]
            }
            """;

        public const string McpJsonNamedWithValue =
            """
            {
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "--host", "localhost"]
                }
              }
            }
            """;

        public const string ServerJsonNamedWithDescription =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "named",
                      "name": "--host",
                      "description": "Database host"
                    },
                  ],
                }
              ]
            }
            """;

        public const string McpJsonNamedWithDescription =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "host",
                  "description": "Database host"
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "--host", "${input:host}"]
                }
              }
            }
            """;

        public const string ServerJsonNamedWithChoices =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "named",
                      "name": "--host",
                      "choices": ["localhost", "azure"]
                    },
                  ],
                }
              ]
            }
            """;

        public const string McpJsonNamedWithChoices =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "host",
                  "description": "",
                  "options": ["localhost", "azure"]
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "--host", "${input:host}"]
                }
              }
            }
            """;

        public const string ServerJsonNamedWithDefault =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "named",
                      "name": "--host",
                      "default": "localhost"
                    },
                  ],
                }
              ]
            }
            """;

        public const string McpJsonNamedWithDefault =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "host",
                  "description": "",
                  "default": "localhost"
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "--host", "${input:host}"]
                }
              }
            }
            """;

        public const string ServerJsonNamedWithNoValueNoDescriptionNoChoicesNoDefault =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "named",
                      "name": "--host"
                    },
                  ],
                }
              ]
            }
            """;

        public const string McpJsonNamedWithNoValueNoDescriptionNoChoicesNoDefault =
            """
            {
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes", "--", "--host"]
                }
              }
            }
            """;

        public const string ServerJsonEnvWithValueVariable =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "environmentVariables": [
                    {
                      "name": "DB_USERNAME",
                      "description": "Database username",
                      "value": "{db_username}",
                      "variables": {
                        "db_username": {
                          "description": "Database username",
                          "isRequired": true
                        }
                      }
                    }
                  ]
                }
              ]
            }
            """;

        public const string McpJsonEnvWithValueVariable =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "db_username",
                  "description": "Database username"
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes"],
                  "env": {
                    "DB_USERNAME": "${input:db_username}"
                  }
                }
              }
            }
            """;

        public const string ServerJsonEnvWithDescriptionNoValue =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "environmentVariables": [
                    {
                      "name": "DB_USERNAME",
                      "description": "Database username"
                    }
                  ]
                }
              ]
            }
            """;

        public const string McpJsonEnvWithDescriptionNoValue =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "DB_USERNAME",
                  "description": "Database username"
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes"],
                  "env": {
                    "DB_USERNAME": "${input:DB_USERNAME}"
                  }
                }
              }
            }
            """;

        public const string ServerJsonEnvWithChoicesNoValue =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "environmentVariables": [
                    {
                      "name": "DB_USERNAME",
                      "choices": ["user", "admin"]
                    }
                  ]
                }
              ]
            }
            """;

        public const string McpJsonEnvWithChoicesNoValue =
            """
            {
              "inputs": [
                {
                  "type": "pickString",
                  "id": "DB_USERNAME",
                  "description": "",
                  "options": ["user", "admin"]
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes"],
                  "env": {
                    "DB_USERNAME": "${input:DB_USERNAME}"
                  }
                }
              }
            }
            """;

        public const string ServerJsonEnvWithDefaultNoValue =
        """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "environmentVariables": [
                    {
                      "name": "DB_USERNAME",
                      "default": "user"
                    }
                  ]
                }
              ]
            }
            """;

        public const string McpJsonEnvWithDefaultNoValue =
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "DB_USERNAME",
                  "description": "",
                  "default": "user"
                }
              ],
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes"],
                  "env": {
                    "DB_USERNAME": "${input:DB_USERNAME}"
                  }
                }
              }
            }
            """;

        public const string ServerJsonEnvWithNoValue =
            """
            {
              "name": "my-db-mcp",
              "description": "my db mcp",
              "repository": {
                "url": "https://github.com/example/my-db-mcp",
                "source": "github",
                "id": "ghi789jk-lmno-1234-pqrs-tuvwxyz56789"
              },
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "environmentVariables": [
                    {
                      "name": "DB_USERNAME"
                    }
                  ]
                }
              ]
            }
            """;

        public const string McpJsonEnvWithNoValue =
            """
            {
              "servers": {
                "Test.McpServer": {
                  "type": "stdio",
                  "command": "dnx",
                  "args": ["Test.McpServer@1.0.0", "--yes"],
                  "env": {
                    "DB_USERNAME": ""
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
              "version": "1.0.0",
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "npm",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [],
                  "environmentVariables": []
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [],
                  "environmentVariables": []
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "",
                  "version": "",
                  "packageArguments": [
                    {
                      "type": "named",
                      "name": "",
                      "description": "",
                      "value": "",
                      "isRepeated": null,
                      "format": "",
                      "choices": [],
                      "isRequired": null
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "",
                  "version": "",
                  "packageArguments": [
                    {
                      "type": "positional",
                      "valueHint": "",
                      "description": "",
                      "value": "",
                      "default": "",
                      "isRepeated": null,
                      "format": "",
                      "choices": [],
                      "isRequired": null
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "",
                  "version": "",
                  "environmentVariables": [
                    {
                      "name": "",
                      "description": "",
                      "default": "",
                      "isRequired": null,
                      "isSecret": null,
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
              "version": "1.0.0"
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
              "version": "1.0.0",
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
              "version": "1.0.0",
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "",
                  "version": "",
                  "packageArguments": [null]
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "",
                  "version": "",
                  "environmentVariables": [null]
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "",
                  "version": "",
                   "packageArguments": [
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
              "version": "1.0.0",
              "packages": [
                {
                  "registryType": "nuget",
                  "name": "example/my-db-mcp",
                  "version": "3.1.0",
                  "packageArguments": [
                    {
                      "type": "",
                      "name": "",
                      "description": "",
                      "value": "",
                      "isRepeated": null,
                      "format": "",
                      "choices": [],
                      "isRequired": null
                    },
                    {
                      "type": "",
                      "valueHint": "",
                      "description": "",
                      "value": "",
                      "default": "",
                      "isRepeated": null,
                      "format": "",
                      "choices": [],
                      "isRequired": null
                    },
                    {
                      "type": "",
                      "description": "",
                      "value": "",
                      "isRequired": null,
                      "isRepeated": null,
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
                  "registryType": "nuget",
                  "environmentVariables": [
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
