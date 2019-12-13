// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;

namespace CatalogTests
{
    internal static class CatalogTestData
    {
        private const string _beforeIndex = @"{{
  ""@id"": ""{0}"",
  ""@type"": [""CatalogRoot"", ""AppendOnlyCatalog"", ""Permalink""],
  ""commitId"": ""fa2a4e80-aab1-434e-926a-6162704c34c8"",
  ""commitTimeStamp"": ""2018-07-16T17:51:57.9718243Z"",
  ""count"": 0,
  ""nuget:lastCreated"": ""2018-07-16T17:23:04.453Z"",
  ""nuget:lastDeleted"": ""2018-07-13T01:15:37Z"",
  ""nuget:lastEdited"": ""2018-07-16T17:25:57.067Z"",
  ""items"": [
  ],
  ""@context"": {{
    ""@vocab"": ""http://schema.nuget.org/catalog#"",
    ""nuget"": ""http://schema.nuget.org/schema#"",
    ""items"": {{
      ""@id"": ""item"",
      ""@container"": ""@set""
    }},
    ""parent"": {{ ""@type"": ""@id"" }},
    ""commitTimeStamp"": {{ ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime"" }},
    ""nuget:lastCreated"": {{ ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime"" }},
    ""nuget:lastEdited"": {{ ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime"" }},
    ""nuget:lastDeleted"": {{ ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime"" }}
  }}
}}";

        private const string _afterIndex = @"{{
  ""@id"": ""{0}"",
  ""@type"": [
    ""CatalogRoot"",
    ""AppendOnlyCatalog"",
    ""Permalink""
  ],
  ""commitId"": ""{1}"",
  ""commitTimeStamp"": ""{2}"",
  ""count"": 1,
  ""nuget:lastCreated"": ""{3}"",
  ""nuget:lastDeleted"": ""{4}"",
  ""nuget:lastEdited"": ""{5}"",
  ""items"": [
    {{
      ""@id"": ""{6}"",
      ""@type"": ""CatalogPage"",
      ""commitId"": ""{1}"",
      ""commitTimeStamp"": ""{2}"",
      ""count"": 1
    }}
  ],
  ""@context"": {{
    ""@vocab"": ""http://schema.nuget.org/catalog#"",
    ""nuget"": ""http://schema.nuget.org/schema#"",
    ""items"": {{
      ""@id"": ""item"",
      ""@container"": ""@set""
    }},
    ""parent"": {{
      ""@type"": ""@id""
    }},
    ""commitTimeStamp"": {{
      ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime""
    }},
    ""nuget:lastCreated"": {{
      ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime""
    }},
    ""nuget:lastEdited"": {{
      ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime""
    }},
    ""nuget:lastDeleted"": {{
      ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime""
    }}
  }}
}}";

        private const string _page = @"{{
  ""@id"": ""{0}"",
  ""@type"": ""CatalogPage"",
  ""commitId"": ""{1}"",
  ""commitTimeStamp"": ""{2}"",
  ""count"": 1,
  ""parent"": ""{3}"",
  ""items"": [
    {{
      ""@id"": ""{4}"",
      ""@type"": ""nuget:PackageDetails"",
      ""commitId"": ""{1}"",
      ""commitTimeStamp"": ""{2}"",
      ""nuget:id"": ""Newtonsoft.Json"",
      ""nuget:version"": ""9.0.2-beta1""
    }}
  ],
  ""@context"": {{
    ""@vocab"": ""http://schema.nuget.org/catalog#"",
    ""nuget"": ""http://schema.nuget.org/schema#"",
    ""items"": {{
      ""@id"": ""item"",
      ""@container"": ""@set""
    }},
    ""parent"": {{
      ""@type"": ""@id""
    }},
    ""commitTimeStamp"": {{
      ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime""
    }},
    ""nuget:lastCreated"": {{
      ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime""
    }},
    ""nuget:lastEdited"": {{
      ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime""
    }},
    ""nuget:lastDeleted"": {{
      ""@type"": ""http://www.w3.org/2001/XMLSchema#dateTime""
    }}
  }}
}}";

        private const string _packageDetails = @"{{
  ""@id"": ""{0}"",
  ""@type"": [
    ""PackageDetails"",
    ""catalog:Permalink""
  ],
  ""authors"": ""James Newton-King"",
  ""catalog:commitId"": ""{1}"",
  ""catalog:commitTimeStamp"": ""{2}"",
  ""created"": ""{3}"",{6}
  ""description"": ""Json.NET is a popular high-performance JSON framework for .NET"",
  ""iconUrl"": ""http://www.newtonsoft.com/content/images/nugeticon.png"",
  ""id"": ""Newtonsoft.Json"",
  ""isPrerelease"": true,
  ""language"": ""en-US"",
  ""lastEdited"": ""{4}"",
  ""licenseUrl"": ""https://raw.github.com/JamesNK/Newtonsoft.Json/master/LICENSE.md"",
  ""listed"": true,
  ""packageHash"": ""bq5DjCtCJpy9R5rsEeQlKz8qGF1Bh3wGaJKMlRwmCoKZ8WUCIFtU3JlyMOdAkSn66KCehCCAxMZFOQD4nNnH/w=="",
  ""packageHashAlgorithm"": ""SHA512"",
  ""packageSize"": 1871318,
  ""projectUrl"": ""http://www.newtonsoft.com/json"",
  ""published"": ""{5}"",
  ""requireLicenseAcceptance"": false,
  ""title"": ""Json.NET"",
  ""verbatimVersion"": ""9.0.2-beta1"",
  ""version"": ""9.0.2-beta1"",
  ""dependencyGroups"": [
    {{
      ""@id"": ""{0}#dependencygroup/.netframework4.5"",
      ""@type"": ""PackageDependencyGroup"",
      ""targetFramework"": "".NETFramework4.5""
    }},
    {{
      ""@id"": ""{0}#dependencygroup/.netframework4.0"",
      ""@type"": ""PackageDependencyGroup"",
      ""targetFramework"": "".NETFramework4.0""
    }},
    {{
      ""@id"": ""{0}#dependencygroup/.netframework3.5"",
      ""@type"": ""PackageDependencyGroup"",
      ""targetFramework"": "".NETFramework3.5""
    }},
    {{
      ""@id"": ""{0}#dependencygroup/.netframework2.0"",
      ""@type"": ""PackageDependencyGroup"",
      ""targetFramework"": "".NETFramework2.0""
    }},
    {{
      ""@id"": ""{0}#dependencygroup/.netportable4.5-profile259"",
      ""@type"": ""PackageDependencyGroup"",
      ""targetFramework"": "".NETPortable4.5-Profile259""
    }},
    {{
      ""@id"": ""{0}#dependencygroup/.netportable4.0-profile328"",
      ""@type"": ""PackageDependencyGroup"",
      ""targetFramework"": "".NETPortable4.0-Profile328""
    }},
    {{
      ""@id"": ""{0}#dependencygroup/.netstandard1.1"",
      ""@type"": ""PackageDependencyGroup"",
      ""dependencies"": [
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/microsoft.csharp"",
          ""@type"": ""PackageDependency"",
          ""id"": ""Microsoft.CSharp"",
          ""range"": ""[4.0.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.collections"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Collections"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.diagnostics.debug"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Diagnostics.Debug"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.dynamic.runtime"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Dynamic.Runtime"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.globalization"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Globalization"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.io"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.IO"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.linq"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Linq"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.linq.expressions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Linq.Expressions"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.objectmodel"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.ObjectModel"",
          ""range"": ""[4.0.12, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.reflection"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Reflection"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.reflection.extensions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Reflection.Extensions"",
          ""range"": ""[4.0.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.resources.resourcemanager"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Resources.ResourceManager"",
          ""range"": ""[4.0.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.runtime"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Runtime"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.runtime.extensions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Runtime.Extensions"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.runtime.numerics"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Runtime.Numerics"",
          ""range"": ""[4.0.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.runtime.serialization.primitives"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Runtime.Serialization.Primitives"",
          ""range"": ""[4.1.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.text.encoding"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Text.Encoding"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.text.encoding.extensions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Text.Encoding.Extensions"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.text.regularexpressions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Text.RegularExpressions"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.threading"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Threading"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.threading.tasks"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Threading.Tasks"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.xml.readerwriter"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Xml.ReaderWriter"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.1/system.xml.xdocument"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Xml.XDocument"",
          ""range"": ""[4.0.11, )""
        }}
      ],
      ""targetFramework"": "".NETStandard1.1""
    }},
    {{
      ""@id"": ""{0}#dependencygroup/.netstandard1.0"",
      ""@type"": ""PackageDependencyGroup"",
      ""dependencies"": [
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/microsoft.csharp"",
          ""@type"": ""PackageDependency"",
          ""id"": ""Microsoft.CSharp"",
          ""range"": ""[4.0.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.collections"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Collections"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.diagnostics.debug"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Diagnostics.Debug"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.dynamic.runtime"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Dynamic.Runtime"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.globalization"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Globalization"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.io"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.IO"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.linq"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Linq"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.linq.expressions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Linq.Expressions"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.objectmodel"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.ObjectModel"",
          ""range"": ""[4.0.12, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.reflection"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Reflection"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.reflection.extensions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Reflection.Extensions"",
          ""range"": ""[4.0.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.resources.resourcemanager"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Resources.ResourceManager"",
          ""range"": ""[4.0.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.runtime"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Runtime"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.runtime.extensions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Runtime.Extensions"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.runtime.serialization.primitives"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Runtime.Serialization.Primitives"",
          ""range"": ""[4.1.1, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.text.encoding"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Text.Encoding"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.text.encoding.extensions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Text.Encoding.Extensions"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.text.regularexpressions"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Text.RegularExpressions"",
          ""range"": ""[4.1.0, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.threading"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Threading"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.threading.tasks"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Threading.Tasks"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.xml.readerwriter"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Xml.ReaderWriter"",
          ""range"": ""[4.0.11, )""
        }},
        {{
          ""@id"": ""{0}#dependencygroup/.netstandard1.0/system.xml.xdocument"",
          ""@type"": ""PackageDependency"",
          ""id"": ""System.Xml.XDocument"",
          ""range"": ""[4.0.11, )""
        }}
      ],
      ""targetFramework"": "".NETStandard1.0""
    }}
  ],
  ""packageEntries"": [
    {{
      ""@id"": ""{0}#Newtonsoft.Json.nuspec"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 793,
      ""fullName"": ""Newtonsoft.Json.nuspec"",
      ""length"": 4359,
      ""name"": ""Newtonsoft.Json.nuspec""
    }},
    {{
      ""@id"": ""{0}#lib/net20/Newtonsoft.Json.dll"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 195596,
      ""fullName"": ""lib/net20/Newtonsoft.Json.dll"",
      ""length"": 489984,
      ""name"": ""Newtonsoft.Json.dll""
    }},
    {{
      ""@id"": ""{0}#lib/net20/Newtonsoft.Json.xml"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 47212,
      ""fullName"": ""lib/net20/Newtonsoft.Json.xml"",
      ""length"": 569142,
      ""name"": ""Newtonsoft.Json.xml""
    }},
    {{
      ""@id"": ""{0}#lib/net35/Newtonsoft.Json.dll"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 183281,
      ""fullName"": ""lib/net35/Newtonsoft.Json.dll"",
      ""length"": 454144,
      ""name"": ""Newtonsoft.Json.dll""
    }},
    {{
      ""@id"": ""{0}#lib/net35/Newtonsoft.Json.xml"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 42857,
      ""fullName"": ""lib/net35/Newtonsoft.Json.xml"",
      ""length"": 512044,
      ""name"": ""Newtonsoft.Json.xml""
    }},
    {{
      ""@id"": ""{0}#lib/net40/Newtonsoft.Json.dll"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 200815,
      ""fullName"": ""lib/net40/Newtonsoft.Json.dll"",
      ""length"": 521728,
      ""name"": ""Newtonsoft.Json.dll""
    }},
    {{
      ""@id"": ""{0}#lib/net40/Newtonsoft.Json.xml"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 44081,
      ""fullName"": ""lib/net40/Newtonsoft.Json.xml"",
      ""length"": 530291,
      ""name"": ""Newtonsoft.Json.xml""
    }},
    {{
      ""@id"": ""{0}#lib/net45/Newtonsoft.Json.dll"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 203659,
      ""fullName"": ""lib/net45/Newtonsoft.Json.dll"",
      ""length"": 532992,
      ""name"": ""Newtonsoft.Json.dll""
    }},
    {{
      ""@id"": ""{0}#lib/net45/Newtonsoft.Json.xml"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 42490,
      ""fullName"": ""lib/net45/Newtonsoft.Json.xml"",
      ""length"": 530291,
      ""name"": ""Newtonsoft.Json.xml""
    }},
    {{
      ""@id"": ""{0}#lib/netstandard1.0/Newtonsoft.Json.dll"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 190807,
      ""fullName"": ""lib/netstandard1.0/Newtonsoft.Json.dll"",
      ""length"": 477184,
      ""name"": ""Newtonsoft.Json.dll""
    }},
    {{
      ""@id"": ""{0}#lib/netstandard1.0/Newtonsoft.Json.xml"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 41114,
      ""fullName"": ""lib/netstandard1.0/Newtonsoft.Json.xml"",
      ""length"": 502998,
      ""name"": ""Newtonsoft.Json.xml""
    }},
    {{
      ""@id"": ""{0}#lib/netstandard1.1/Newtonsoft.Json.dll"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 192147,
      ""fullName"": ""lib/netstandard1.1/Newtonsoft.Json.dll"",
      ""length"": 480768,
      ""name"": ""Newtonsoft.Json.dll""
    }},
    {{
      ""@id"": ""{0}#lib/netstandard1.1/Newtonsoft.Json.xml"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 41114,
      ""fullName"": ""lib/netstandard1.1/Newtonsoft.Json.xml"",
      ""length"": 502998,
      ""name"": ""Newtonsoft.Json.xml""
    }},
    {{
      ""@id"": ""{0}#lib/portable-net40%2Bsl5%2Bwp80%2Bwin8%2Bwpa81/Newtonsoft.Json.dll"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 169713,
      ""fullName"": ""lib/portable-net40%2Bsl5%2Bwp80%2Bwin8%2Bwpa81/Newtonsoft.Json.dll"",
      ""length"": 425984,
      ""name"": ""Newtonsoft.Json.dll""
    }},
    {{
      ""@id"": ""{0}#lib/portable-net40%2Bsl5%2Bwp80%2Bwin8%2Bwpa81/Newtonsoft.Json.xml"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 38591,
      ""fullName"": ""lib/portable-net40%2Bsl5%2Bwp80%2Bwin8%2Bwpa81/Newtonsoft.Json.xml"",
      ""length"": 478486,
      ""name"": ""Newtonsoft.Json.xml""
    }},
    {{
      ""@id"": ""{0}#lib/portable-net45%2Bwp80%2Bwin8%2Bwpa81/Newtonsoft.Json.dll"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 190828,
      ""fullName"": ""lib/portable-net45%2Bwp80%2Bwin8%2Bwpa81/Newtonsoft.Json.dll"",
      ""length"": 476672,
      ""name"": ""Newtonsoft.Json.dll""
    }},
    {{
      ""@id"": ""{0}#lib/portable-net45%2Bwp80%2Bwin8%2Bwpa81/Newtonsoft.Json.xml"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 40872,
      ""fullName"": ""lib/portable-net45%2Bwp80%2Bwin8%2Bwpa81/Newtonsoft.Json.xml"",
      ""length"": 502998,
      ""name"": ""Newtonsoft.Json.xml""
    }},
    {{
      ""@id"": ""{0}#tools/install.ps1"",
      ""@type"": ""PackageEntry"",
      ""compressedLength"": 1244,
      ""fullName"": ""tools/install.ps1"",
      ""length"": 3852,
      ""name"": ""install.ps1""
    }}
  ],
  ""tags"": [
    ""json""
  ],
  ""@context"": {{
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""catalog"": ""http://schema.nuget.org/catalog#"",
    ""xsd"": ""http://www.w3.org/2001/XMLSchema#"",
    ""dependencies"": {{
      ""@id"": ""dependency"",
      ""@container"": ""@set""
    }},
    ""dependencyGroups"": {{
      ""@id"": ""dependencyGroup"",
      ""@container"": ""@set""
    }},
    ""packageEntries"": {{
      ""@id"": ""packageEntry"",
      ""@container"": ""@set""
    }},
    ""packageTypes"": {{
      ""@id"": ""packageType"",
      ""@container"": ""@set""
    }},
    ""supportedFrameworks"": {{
      ""@id"": ""supportedFramework"",
      ""@container"": ""@set""
    }},
    ""tags"": {{
      ""@id"": ""tag"",
      ""@container"": ""@set""
    }},
    ""published"": {{
      ""@type"": ""xsd:dateTime""
    }},
    ""created"": {{
      ""@type"": ""xsd:dateTime""
    }},
    ""lastEdited"": {{
      ""@type"": ""xsd:dateTime""
    }},
    ""catalog:commitTimeStamp"": {{
      ""@type"": ""xsd:dateTime""
    }},
    ""reasons"": {{
      ""@container"": ""@set""
    }}
  }}
}}";

        private const string _packageDeprecationDetails = @"
  ""deprecation"": {{
    ""@id"": ""{0}#deprecation"",{1}{2}
    ""reasons"": [{3}]
  }},";

        private const string _packageDeprecationAlternatePackageDetails = @"
    ""alternatePackage"": {{
      ""@id"": ""{0}#deprecation/alternatePackage"",
      ""id"": ""theId"",
      ""range"": ""{1}""
    }},";

        private const string _packageDeprecationMessageDetails = @"
    ""message"": ""this is the message"",";

        internal static JObject GetBeforeIndex(Uri indexUri)
        {
            return JObject.Parse(string.Format(_beforeIndex, indexUri));
        }

        internal static JObject GetAfterIndex(
            Uri indexUri,
            Guid commitId,
            DateTime commitTimestamp,
            DateTime lastCreated,
            DateTime lastDeleted,
            DateTime lastEdited,
            Uri pageUri)
        {
            return JObject.Parse(
                string.Format(
                    _afterIndex,
                    indexUri,
                    commitId.ToString(),
                    commitTimestamp.ToString("O"),
                    lastCreated.ToString("O"),
                    lastDeleted.ToString("O"),
                    lastEdited.ToString("O"),
                    pageUri));
        }

        internal static JObject GetPage(
            Uri pageUri,
            Guid commitId,
            DateTime commitTimestamp,
            Uri indexUri,
            Uri packageDetailsUri)
        {
            return JObject.Parse(
                string.Format(
                    _page,
                    pageUri,
                    commitId.ToString(),
                    commitTimestamp.ToString("O"),
                    indexUri,
                    packageDetailsUri));
        }

        internal static JObject GetPackageDetails(
            Uri packageDetailsUri,
            Guid commitId,
            DateTime commitTimestamp,
            DateTime created,
            DateTime lastEdited,
            DateTime published,
            PackageDeprecationItem deprecation)
        {
            return JObject.Parse(
                string.Format(
                    _packageDetails,
                    packageDetailsUri,
                    commitId.ToString(),
                    commitTimestamp.ToString("O"),
                    created.ToString("O"),
                    lastEdited.ToString("O"),
                    published.ToString("O"),
                    GetPackageDeprecationDetails(packageDetailsUri, deprecation)));
        }

        private static string GetPackageDeprecationDetails(
            Uri packageDetailsUri,
            PackageDeprecationItem deprecation)
        {
            if (deprecation == null)
            {
                return string.Empty;
            }

            return string.Format(
                _packageDeprecationDetails,
                packageDetailsUri,
                GetPackageDeprecationAlternatePackageDetails(packageDetailsUri, deprecation),
                deprecation.Message == null ? string.Empty : _packageDeprecationMessageDetails,
                string.Join(",", deprecation.Reasons.Select(r => $"\r\n      \"{r}\"")) + "\r\n    ");
        }

        private static string GetPackageDeprecationAlternatePackageDetails(
            Uri packageDetailsUri,
            PackageDeprecationItem deprecation)
        {
            if (deprecation.AlternatePackageId == null)
            {
                return string.Empty;
            }

            return string.Format(
                _packageDeprecationAlternatePackageDetails,
                packageDetailsUri,
                deprecation.AlternatePackageRange);
        }
    }
}