using GitHubAdvisoryTransformer;
using GitHubAdvisoryTransformer.Collector;
using GitHubAdvisoryTransformer.Cursor;
using GitHubAdvisoryTransformer.Entities;
using GitHubAdvisoryTransformer.GraphQL;
using GitHubAdvisoryTransformer.Ingest;
using Newtonsoft.Json;


var configuration = new GitHubAdvisoryTransformerConfiguration
{
    GitHubPersonalAccessToken = ""
};

if (args.Length > 0)
{
    configuration.GitHubPersonalAccessToken = args[0];
}

var devBaseUrl = "https://apidev.nugettest.org/";
var intBaseUrl = "https://apiint.nugettest.org/";
var prodBaseUrl = "https://api.nuget.org/";

var vulnerabilitiesFilePath = "v3/vulnerabilities/";

var indexFileName = "index.json";
var baseFileName = "vulnerability.base.json";
var updateFileName = "vulnerability.update.json";

var httpClient = new HttpClient();

var writer = new JsonRangeVulnerabilityWriter(baseFileName);
var githubVersionParser = new GitHubVersionRangeParser();
var ingestor = new AdvisoryIngestor(writer, githubVersionParser);

var cursor = new ReadWriteCursor<DateTimeOffset>();
cursor.Value = DateTimeOffset.UnixEpoch;

var githubQueryService = new QueryService(configuration, httpClient);
var queryBuilder = new AdvisoryQueryBuilder();

var advisoryQueryService = new AdvisoryQueryService(githubQueryService, queryBuilder);

var collector = new AdvisoryCollector(cursor, advisoryQueryService, ingestor);

await collector.ProcessAsync(CancellationToken.None);

var today =DateTime.UtcNow;

var devIndex = new IndexEntry[] { 
    new IndexEntry
    {
        Name="base",
        Id=devBaseUrl + vulnerabilitiesFilePath + baseFileName,
        Updated=today,
        Comment="The base data for vulnerability update periodically"
    },
    new IndexEntry
    {
        Name="update",
        Id=devBaseUrl + vulnerabilitiesFilePath + updateFileName,
        Updated=today,
        Comment="The patch data for the vulnerability. Contains all the vulnerabilities since base was last updated."
    },
};

var intIndex = new IndexEntry[] {
    new IndexEntry
    {
        Name="base",
        Id=intBaseUrl + vulnerabilitiesFilePath + baseFileName,
        Updated=today,
        Comment="The base data for vulnerability update periodically"
    },
    new IndexEntry
    {
        Name="update",
        Id=intBaseUrl + vulnerabilitiesFilePath + updateFileName,
        Updated=today,
        Comment="The patch data for the vulnerability. Contains all the vulnerabilities since base was last updated."
    },
};

var prodIndex = new IndexEntry[] {
    new IndexEntry
    {
        Name="base",
        Id=prodBaseUrl + vulnerabilitiesFilePath + baseFileName,
        Updated=today,
        Comment="The base data for vulnerability update periodically"
    },
    new IndexEntry
    {
        Name="update",
        Id=prodBaseUrl + vulnerabilitiesFilePath + updateFileName,
        Updated=today,
        Comment="The patch data for the vulnerability. Contains all the vulnerabilities since base was last updated."
    },
};

var devFileContents = JsonConvert.SerializeObject(devIndex, Formatting.Indented);
var intFileContents = JsonConvert.SerializeObject(intIndex, Formatting.Indented);
var prodFileContents = JsonConvert.SerializeObject(prodIndex, Formatting.Indented);

Directory.CreateDirectory("DEV/");
Directory.CreateDirectory("INT/");
Directory.CreateDirectory("PROD/");

File.WriteAllText("DEV/" + indexFileName, devFileContents);
File.WriteAllText("INT/" + indexFileName, intFileContents);
File.WriteAllText("PROD/" + indexFileName, prodFileContents);