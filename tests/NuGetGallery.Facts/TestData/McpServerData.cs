// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.TestData
{
    internal static class McpServerData
    {
        public const string ServerJsonNoNugetRegistry = @"
{
    ""name"": ""io.github.yourusername/your-repository"",
    ""description"": ""Your MCP server description"",
    ""version_detail"": {
    ""version"": ""1.0.0""
    },
    ""packages"": [
    {
        ""registry_name"": ""npm"",
        ""name"": ""your-npm-package"",
        ""version"": ""1.0.0"",
        ""package_arguments"": [
        {
            ""description"": ""Specify services and permissions"",
            ""is_required"": true,
            ""format"": ""string"",
            ""value"": ""-s"",
            ""default"": ""-s"",
            ""type"": ""positional"",
            ""value_hint"": ""-s""
        }
        ],
        ""environment_variables"": [
        {
            ""name"": ""API_KEY"",
            ""description"": ""API Key to access the server""
        }
        ]
    }
    ],
    ""repository"": {
    ""url"": ""https://github.com/yourusername/your-repository"",
    ""source"": ""github""
    }
}";

        public const string ServerJsonValid = @"
{
    ""name"": ""io.github.yourusername/your-repository"",
    ""description"": ""Your MCP server description"",
    ""version_detail"": {
    ""version"": ""1.0.0""
    },
    ""packages"": [
    {
        ""registry_name"": ""npm"",
        ""name"": ""your-npm-package"",
        ""version"": ""1.0.0"",
        ""package_arguments"": [
        {
            ""description"": ""Specify services and permissions"",
            ""is_required"": true,
            ""format"": ""string"",
            ""value"": ""-s"",
            ""default"": ""-s"",
            ""type"": ""positional"",
            ""value_hint"": ""-s""
        }
        ],
        ""environment_variables"": [
        {
            ""name"": ""API_KEY"",
            ""description"": ""API Key to access the server""
        }
        ]
    }
    ],
    ""repository"": {
    ""url"": ""https://github.com/yourusername/your-repository"",
    ""source"": ""github""
    }
}";

        public const string ServerJsonMinified = @"
{""name"":""io.github.yourusername/your-repository"",""description"":""Your MCP server description"",""version_detail"":{""version"":""1.0.0""},""packages"":[{""registry_name"":""nuget"",""name"":""your-npm-package"",""version"":""1.0.0"",""package_arguments"":[{""description"":""Specify services and permissions"",""is_required"":true,""format"":""string"",""value"":""-s"",""default"":""-s"",""type"":""positional"",""value_hint"":""-s""}],""environment_variables"":[{""name"":""API_KEY"",""description"":""API Key to access the server""}]}],""repository"":{""url"":""https://github.com/yourusername/your-repository"",""source"":""github""}}";

        public const string McpJsonValid = @"
{
  ""inputs"": [
    {
      ""type"": ""promptString"",
      ""id"": ""input-0"",
      ""description"": ""API Key to access the server"",
      ""password"": true
    },
    {
      ""type"": ""promptString"",
      ""id"": ""input-1"",
      ""description"": ""Specify services and permissions"",
      ""password"": true
    }
  ],
  ""servers"": {
    ""Japarson.Mcp.1"": {
      ""type"": ""stdio"",
      ""command"": ""dnx"",
      ""args"": [""Japarson.Mcp.1"", ""--"", ""mcp"", ""start""],
      ""env"": {
        ""API_KEY"": ""${input:input-0}""
      }
    }
  }
}";

        public const string ServerJsonNoArgsAndEnv = @"
{
    ""name"": ""io.github.yourusername/your-repository"",
    ""description"": ""Your MCP server description"",
    ""version_detail"": {
    ""version"": ""1.0.0""
    },
    ""packages"": [
    {
        ""registry_name"": ""npm"",
        ""name"": ""your-npm-package"",
        ""version"": ""1.0.0"",
        ""package_arguments"": [],
        ""environment_variables"": []
    }
    ],
    ""repository"": {
    ""url"": ""https://github.com/yourusername/your-repository"",
    ""source"": ""github""
    }
}";

        public const string McpJsonNoArgsAndEnv = @"
{
  ""servers"": {
    ""Japarson.Mcp.1"": {
      ""type"": ""stdio"",
      ""command"": ""dnx"",
      ""args"": [""Japarson.Mcp.1"", ""--"", ""mcp"", ""start""]
    }
  }
}";
    }
}
