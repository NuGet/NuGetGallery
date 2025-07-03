// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.TestData
{
    internal static class McpServerData
    {
        public const string ServerJsonValid = @"{
  ""name"": ""my-db-mcp"",
  ""description"": ""my db mcp"",
  ""repository"": {
    ""url"": ""https://github.com/example/my-db-mcp"",
    ""source"": ""github"",
    ""id"": ""ghi789jk-lmno-1234-pqrs-tuvwxyz56789""
  },
  ""version_detail"": {
    ""version"": ""3.1.0"",
    ""release_date"": ""2024-03-05T16:45:00Z""
  },
  ""packages"": [
    {
      ""registry_name"": ""nuget"",
      ""name"": ""example/my-db-mcp"",
      ""version"": ""3.1.0"",
      ""package_arguments"": [
        {
          ""type"": ""named"",
          ""name"": ""--host"",
          ""description"": ""Database host"",
          ""default"": ""localhost"",
          ""is_required"": true
        },
        {
          ""type"": ""named"",
          ""name"": ""--port"",
          ""description"": ""Database port"",
          ""format"": ""number""
        },
        {
          ""type"": ""positional"",
          ""value_hint"": ""database_name"",
          ""description"": ""Name of the database to connect to"",
          ""is_required"": true
        }
      ],
      ""environment_variables"": [
        {
          ""name"": ""DB_USERNAME"",
          ""description"": ""Database username"",
          ""is_required"": true
        },
        {
          ""name"": ""DB_PASSWORD"",
          ""description"": ""Database password"",
          ""is_required"": true,
          ""is_secret"": true
        },
        {
          ""name"": ""SSL_MODE"",
          ""description"": ""SSL connection mode"",
          ""default"": ""prefer"",
          ""choices"": [""disable"", ""prefer"", ""require""]
        }
      ]
    }
  ]
}";

        public const string ServerJsonValidMinified = @"{""name"":""my-db-mcp"",""description"":""my db mcp"",""repository"":{""url"":""https://github.com/example/my-db-mcp"",""source"":""github"",""id"":""ghi789jk-lmno-1234-pqrs-tuvwxyz56789""},""version_detail"":{""version"":""3.1.0"",""release_date"":""2024-03-05T16:45:00Z""},""packages"":[{""registry_name"":""nuget"",""name"":""example/my-db-mcp"",""version"":""3.1.0"",""package_arguments"":[{""type"":""named"",""name"":""--host"",""description"":""Database host"",""default"":""localhost"",""is_required"":true},{""type"":""named"",""name"":""--port"",""description"":""Database port"",""format"":""number""},{""type"":""positional"",""value_hint"":""database_name"",""description"":""Name of the database to connect to"",""is_required"":true}],""environment_variables"":[{""name"":""DB_USERNAME"",""description"":""Database username"",""is_required"":true},{""name"":""DB_PASSWORD"",""description"":""Database password"",""is_required"":true,""is_secret"":true},{""name"":""SSL_MODE"",""description"":""SSL connection mode"",""default"":""prefer"",""choices"":[""disable"",""prefer"",""require""]}]}]}";

        public const string McpJsonValid = @"{
  ""inputs"": [
    {
      ""type"": ""promptString"",
      ""id"": ""input-1"",
      ""description"": ""Database username"",
      ""password"": false
    },
    {
      ""type"": ""promptString"",
      ""id"": ""input-2"",
      ""description"": ""Database password"",
      ""password"": true
    },
    {
      ""type"": ""pickString"",
      ""id"": ""input-3"",
      ""description"": ""SSL connection mode"",
      ""password"": false,
      ""default"": ""prefer"",
      ""choices"": [""disable"", ""prefer"", ""require""]
    },
    {
      ""type"": ""promptString"",
      ""id"": ""input-4"",
      ""description"": ""Database host"",
      ""password"": false
    },
    {
      ""type"": ""promptString"",
      ""id"": ""input-5"",
      ""description"": ""Database port"",
      ""password"": false
    },
    {
      ""type"": ""promptString"",
      ""id"": ""input-6"",
      ""description"": ""Name of the database to connect to"",
      ""password"": false
    }
  ],
  ""servers"": {
    ""%%PACKAGE_ID_PLACEHOLDER%%"": {
      ""type"": ""stdio"",
      ""command"": ""dnx"",
      ""args"": [""%%PACKAGE_ID_PLACEHOLDER%%"", ""--"", ""mcp"", ""start""],
      ""env"": {
        ""DB_USERNAME"": ""${input:input-1}"",
        ""DB_PASSWORD"": ""${input:input-2}"",
        ""SSL_MODE"": ""${input:input-3}""
      }
    }
  }
}";

        public const string ServerJsonNoPackages = @"{
  ""name"": ""my-db-mcp"",
  ""description"": ""my db mcp"",
  ""repository"": {
    ""url"": ""https://github.com/example/my-db-mcp"",
    ""source"": ""github"",
    ""id"": ""ghi789jk-lmno-1234-pqrs-tuvwxyz56789""
  },
  ""version_detail"": {
    ""version"": ""3.1.0"",
    ""release_date"": ""2024-03-05T16:45:00Z""
  },
  ""packages"": []
}";

        public const string ServerJsonNoNugetRegistry = @"{
  ""name"": ""my-db-mcp"",
  ""description"": ""my db mcp"",
  ""repository"": {
    ""url"": ""https://github.com/example/my-db-mcp"",
    ""source"": ""github"",
    ""id"": ""ghi789jk-lmno-1234-pqrs-tuvwxyz56789""
  },
  ""version_detail"": {
    ""version"": ""3.1.0"",
    ""release_date"": ""2024-03-05T16:45:00Z""
  },
  ""packages"": [
    {
      ""registry_name"": ""npm"",
      ""name"": ""example/my-db-mcp"",
      ""version"": ""3.1.0"",
      ""package_arguments"": [],
      ""environment_variables"": []
    }
  ]
}";

        public const string ServerJsonNoArgsAndEnv = @"{
  ""name"": ""my-db-mcp"",
  ""description"": ""my db mcp"",
  ""repository"": {
    ""url"": ""https://github.com/example/my-db-mcp"",
    ""source"": ""github"",
    ""id"": ""ghi789jk-lmno-1234-pqrs-tuvwxyz56789""
  },
  ""version_detail"": {
    ""version"": ""3.1.0"",
    ""release_date"": ""2024-03-05T16:45:00Z""
  },
  ""packages"": [
    {
      ""registry_name"": ""nuget"",
      ""name"": ""example/my-db-mcp"",
      ""version"": ""3.1.0"",
      ""package_arguments"": [],
      ""environment_variables"": []
    }
  ]
}";

        public const string McpJsonNoArgsAndEnv = @"{
  ""inputs"": [],
  ""servers"": {
    ""%%PACKAGE_ID_PLACEHOLDER%%"": {
      ""type"": ""stdio"",
      ""command"": ""dnx"",
      ""args"": [""%%PACKAGE_ID_PLACEHOLDER%%"", ""--"", ""mcp"", ""start""],
      ""env"": {}
    }
  }
}";
    }
}
