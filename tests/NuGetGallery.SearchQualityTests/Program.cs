using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGetGallery;
using System.IO;
using Moq;
using Xunit;

namespace SearchQuality
{
    class PseudoDependency
    {
        public string TargetFramework { get; set; }
        public string Version { get; set; }
    }

    class PseudoPackageRegistration
    {
        public int DownloadCount { get; set; }
        public string Id { get; set; }
        public int Key { get; set; }
        public List<string> Owners { get; set; }
    }

    class PseudoPackage
    {
        public List<string> Authors { get; set; }
        public string Copyright { get; set; }
        public DateTime Created { get; set; }
        public List<PseudoDependency> Dependencies { get; set; }
        public string Description { get; set; }
        public int DownloadCount { get; set; }
        public string FlattenedAuthors { get; set; }
        public string FlattenedDependencies { get; set; }
        public string Hash { get; set; }
        public string HashAlgorithm { get; set; }
        public string IconUrl { get; set; }
        public bool IsLatest { get; set; }
        public bool IsLatestStable { get; set; }
        public bool IsPrerelease { get; set; }
        public int Key { get; set; }
        public string Language { get; set; }
        public DateTime LastUpdated { get; set; }
        public string LicenseUrl { get; set; }
        public bool Listed { get; set; }
        public string MinClientVersion { get; set; }
        public int PackageFileSize { get; set; }
        public PseudoPackageRegistration PackageRegistration { get; set; }
        public int PackageRegistrationKey { get; set; }
        public string ProjectUrl { get; set; }
        public DateTime Published { get; set; }
        public string ReleaseNotes { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public List<string> SupportedFrameworks { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
    }

    class Program
    {
        static LuceneSearchService luceneSearchService;

        static void Main(string[] args)
        {
            var packageDataJson = File.ReadAllText(@"samplePackageData.json");
            var asArray = JArray.Parse(packageDataJson);
            List<Package> packages = LoadPackages(asArray);

            Lucene.Net.Store.Directory d = new Lucene.Net.Store.RAMDirectory();
            var packageSource = new Mock<IPackageSource>();
            packageSource
                .Setup(ps => ps.GetPackagesForIndexing(It.IsAny<DateTime?>()))
                .Returns(new PackageIndexEntity[0].AsQueryable());

            var luceneIndexingService = new LuceneIndexingService(packageSource.Object, d);
            luceneIndexingService.UpdateIndex(forceRefresh: true);
            luceneIndexingService.AddPackages(packages.Select(p => new PackageIndexEntity(p)).ToList(), true);
            luceneSearchService = new LuceneSearchService(d);

            TestSearch("jQuery", "jQuery", 1);
            TestSearch("jqueryui", "jQuery.UI.Combined", 4);

            TestSearch("EntityFramework", "EntityFramework", 1);
            TestSearch("Entity Framework", "EntityFramework", 1);
            TestSearch("entity", "EntityFramework", 1);
            //TestSearch("EF", "EntityFramework", 10); //Currently it's not high in the results AT ALL. Should it be?

            TestSearch("Windows.Azure.Storage", "WindowsAzure.Storage", 1);
            TestSearch("Windows Azure Storage", "WindowsAzure.Storage", 1);
            TestSearch("Azure Storage", "WindowsAzure.Storage", 1);

            TestSearch("Windows.Azure.Caching", "Microsoft.WindowsAzure.Caching", 1);
            TestSearch("Windows Azure Caching", "Microsoft.WindowsAzure.Caching", 1);
            TestSearch("Azure Caching", "Microsoft.WindowsAzure.Caching", 1);
            TestSearch("Azure Cache", "Microsoft.WindowsAzure.Caching", 1);
            TestSearch("Windows Azure Cache", "Microsoft.WindowsAzure.Caching", 1);

            // These 3 popular 'service bus' packages are all in top 5 at time of building this corpus
            // Actually NServiceBus has the most downloads, but is lowest ranked for some reason, well, that might change some day.
            TestSearch("NServiceBus", "NServiceBus", 5);
            TestSearch("Rhino.ServiceBus", "Rhino.ServiceBus", 5);
            TestSearch("ServiceBus", "WindowsAzure.ServiceBus", 5);

            // By FAR the most popular json package
            TestSearch("json", "NewtonSoft.Json", 1);
            TestSearch("Json.net", "NewtonSoft.Json", 1);
            TestSearch("Newtonsoft", "NewtonSoft.Json", 1);

            // A popular json javascript package
            TestSearch("json javascript", "json2", 1);
            TestSearch("json js", "json2", 1);

            // Some other results with justifiably good relevance for json include the 'json' package, and ServiceStack.Text 
            TestSearch("json", "json", 10);
            TestSearch("json", "ServiceStack.Text", 10);

            TestSearch("Microsoft web api", "Microsoft.AspNet.WebApi", 3);

            TestSearch("microsoft http", "Microsoft.AspNet.WebApi", 4);
            TestSearch("microsoft http", "Microsoft.AspNet.WebApi.WebHost", 4);
            TestSearch("microsoft http", "Microsoft.Net.Http", 4);
            TestSearch("microsoft http", "Microsoft.Web.Infrastructure", 4);

            // Finding some misc packages by their distinctive ids
            TestSearch("Hammock", "Hammock", 1);
            TestSearch("WebActivator", "WebActivatorEx", 1);
            TestSearch("Modernizr", "Modernizr", 1);
            TestSearch("SimpleInjector", "SimpleInjector", 1);
            TestSearch("Simple Injector", "SimpleInjector", 1);

            // Some more general term looking searches
            TestSearch("Asp.net Mvc", "Microsoft.AspNet.Mvc", 2); //unbelievably it's losing to elmah...
            TestSearch("asp.net web pages", "Microsoft.AspNet.WebPages", 1);
            TestSearch("logging", "Elmah", 1);
            TestSearch("search", "Lucene.Net", 1);
            TestSearch("lucene", "Lucene.Net", 1);
            TestSearch("search", "NHibernate.Search", 20);
            TestSearch("hibernate", "NHibernate", 1);
            TestSearch("hibernate", "FluentNHibernate", 2);
            TestSearch("hibernate profiler", "NHibernateProfiler", 4);
            TestSearch("sql profiler", "MiniProfiler", 1);
            TestSearch("sql profiler", "LinqToSqlProfiler", 5);
            TestSearch("haacked", "RouteMagic", 2);
            TestSearch("haacked", "MVcHaack.Ajax", 4);
            TestSearch("haacked", "WebBackgrounder", 5);
            TestSearch("haacked", "routedebugger", 5);
            
            TestSearch("NuGet", "NuGet.Core", 3);
            TestSearch("NuGet", "NuGet.CommandLine", 2);
            TestSearch("NuGet", "NuGet.Build", 3);

            TestSearch("Mock", "RhinoMocks", 4);
            TestSearch("Mock", "NUnit.Mocks", 4);
            TestSearch("Mock", "Moq", 4);
            TestSearch("Mock", "FakeItEasy", 20);
            TestSearch("Mock", "Ninject.MockingKernel", 30);
            TestSearch("Mock", "AutoWrockTestable", 30);
            TestSearch("Mock", "Nukito", 40);
            TestSearch("Mock", "MockJockey", 50);
            TestSearch("Mock", "Machine.Specifications.AutoMocking", 50);
            TestSearch("Mock", "WP-Fx.EasyMoq", 60);

            TestSearch("razor", "Microsoft.AspNet.Razor", 1);
            TestSearch("razor 2", "Microsoft.AspNet.Razor", 1);
            TestSearch("memes", "FourOne.Memes", 1);
            TestSearch("ninject", "Ninject", 1);
            TestSearch("nunit", "NUnit", 1);
            TestSearch("testing", "NUnit", 1);
            TestSearch("testing", "xunit", 30); //It should probably be higher
            TestSearch("Asp.net MVC scaffolding", "MvcScaffolding", 3);
            TestSearch("Asp.net MVC scaffolding", "NLibScaffolding", 90);
            TestSearch("Asp.net MVC scaffolding", "WijmoMvcScaffolding", 120);
            TestSearch("Asp.net MVC scaffolding", "MvcScaffolding.Obsidian", 130);
            TestSearch("mvc scaffold", "MvcScaffolding", 8);
            TestSearch("mvc scaffold", "ModelScaffolding", 20);
            TestSearch("Microsoft web infrastructure", "Microsoft.Web.Infrastructure", 1);
            TestSearch("Dotnetopenauth", "DotNetOpenAuth", 1);
            TestSearch("OpenID", "DotNetOpenAuth.OpenId.Core", 1);
            TestSearch("image resizer", "ImageResizer", 1);
            TestSearch("parsing", "CommandLineParser", 10);
            TestSearch("knockoutjs", "knockoutjs", 1);
            TestSearch("knockout js", "knockoutjs", 3); //it should probably be higher, i.e. beating json.net
            // TestSearch("knockout.js", "knockoutjs", 1); // fails to find it
            TestSearch("helpers", "microsoft-web-helpers", 1);
            TestSearch("fluent mongo", "FluentMongo", 1);
            TestSearch("fluent mongo", "MongoFluentUpdater", 15);
            TestSearch("mongo", "mongocsharpdriver", 1);
            TestSearch("mongo", "FluentMongo", 2);
            TestSearch("mongo elmah", "elmah.mongodb", 15); //should be higher?

            // These guys are by far the most popular DI packages
            TestSearch("injection", "Unity", 2);
            TestSearch("injection", "Ninject", 2);
            TestSearch("dependency injection", "Unity", 2);
            TestSearch("dependency injection", "Ninject", 2);
        }

        static void TestSearch(string query, string expectedPackageId, int maxExpectedPosition)
        {
            var searchFilter = new SearchFilter
            {
                Skip = 0,
                Take = maxExpectedPosition,
                SearchTerm = query,
            };

            int totalHits;
            var results = luceneSearchService.Search(searchFilter, out totalHits).ToList();

            Assert.NotEqual(0, results.Count);
            Assert.Contains(expectedPackageId, results.Select(p => p.PackageRegistration.Id), StringComparer.InvariantCultureIgnoreCase);
            Assert.True(results.Count <= maxExpectedPosition);
        }

        static List<Package> LoadPackages(JArray asArray)
        {
            var packages = new List<Package>();
            Dictionary<string, User> users = new Dictionary<string, User>();

            Func<string, User> getUser = (name) =>
            {
                if (!users.ContainsKey(name))
                {
                    users[name] = new User { Username = name };
                }
                return users[name];
            };

            Func<PseudoDependency, PackageDependency> getDependency = (pd) =>
            {
                return new PackageDependency
                {
                    TargetFramework = pd.TargetFramework,
                    VersionSpec = pd.Version,
                };
            };

            Func<PseudoPackageRegistration, PackageRegistration> getPackageRegistration = (pr) =>
            {
                return new PackageRegistration
                {
                    DownloadCount = pr.DownloadCount,
                    Id = pr.Id,
                    Key = pr.Key,
                    Owners = pr.Owners.Select(name => getUser(name)).ToList(),
                };
            };

            foreach (JObject j in asArray)
            {
                PseudoPackage pseudo = j.ToObject<PseudoPackage>();
                Package p = new Package
                {
                    Authors = pseudo.Authors.Select(name => new PackageAuthor { Name = name }).ToList(),
                    Copyright = pseudo.Copyright,
                    Created = pseudo.Created,
                    Description = pseudo.Description,
                    Dependencies = pseudo.Dependencies.Select(pd => getDependency(pd)).ToList(),
                    DownloadCount = pseudo.DownloadCount,
                    FlattenedAuthors = pseudo.FlattenedAuthors,
                    FlattenedDependencies = pseudo.FlattenedDependencies,
                    Hash = pseudo.Hash,
                    HashAlgorithm = pseudo.HashAlgorithm,
                    IconUrl = pseudo.IconUrl,
                    IsLatest = pseudo.IsLatest,
                    IsLatestStable = pseudo.IsLatestStable,
                    IsPrerelease = pseudo.IsPrerelease,
                    Key = pseudo.Key,
                    Language = pseudo.Language,
                    LastUpdated = pseudo.LastUpdated,
                    LicenseUrl = pseudo.LicenseUrl,
                    Listed = pseudo.Listed,
                    MinClientVersion = pseudo.MinClientVersion,
                    PackageFileSize = pseudo.PackageFileSize,
                    PackageRegistration = getPackageRegistration(pseudo.PackageRegistration),
                    PackageRegistrationKey = pseudo.PackageRegistrationKey,
                    ProjectUrl = pseudo.ProjectUrl,
                    Published = pseudo.Published,
                    ReleaseNotes = pseudo.ReleaseNotes,
                    RequiresLicenseAcceptance = pseudo.RequiresLicenseAcceptance,
                    Summary = pseudo.Summary,
                    SupportedFrameworks = pseudo.SupportedFrameworks.Select(tf => new PackageFramework { TargetFramework = tf }).ToList(),
                    Tags = pseudo.Tags,
                    Title = pseudo.Title,
                    Version = pseudo.Version,
                };

                packages.Add(p);
            }

            return packages;
        }

        static void GeneratePackageDataFile(string[] args)
        {
            string connectionString = @"Data Source=(LocalDB)\v11.0;Initial Catalog=SearchTesting;Integrated Security=SSPI";
            var context = new EntitiesContext(connectionString, readOnly: false);
            var packageRepo = new EntityRepository<Package>(context);

            var packages = packageRepo.GetAll()
                .Where(p => p.IsLatest || p.IsLatestStable)  // which implies that p.IsListed by the way!
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks).ToList();

            var ja = new JArray();
            foreach (Package p in packages)
            {
                var pseudoPackage = new
                {
                    Authors = p.Authors.Select(pa => pa.Name).ToList(),
                    p.Copyright,
                    p.Created,
                    Dependencies = p.Dependencies.Select(pd => new { pd.TargetFramework, pd.VersionSpec }).ToList(),
                    p.Description,
                    p.DownloadCount,
                    p.FlattenedAuthors,
                    p.FlattenedDependencies,
                    p.Hash,
                    p.HashAlgorithm,
                    p.IconUrl,
                    p.IsLatest,
                    p.IsLatestStable,
                    p.IsPrerelease,
                    p.Key,
                    p.Language,
                    p.LastUpdated,
                    p.LicenseUrl,
                    p.Listed,
                    p.MinClientVersion,
                    p.PackageFileSize,
                    PackageRegistration = new
                    {
                        DownloadCount = p.PackageRegistration.DownloadCount,
                        Id = p.PackageRegistration.Id,
                        Key = p.PackageRegistration.Key,
                        Owners = p.PackageRegistration.Owners.Select(o => o.Username).ToList(),
                    },
                    p.PackageRegistrationKey,
                    p.ProjectUrl,
                    p.Published,
                    p.ReleaseNotes,
                    p.RequiresLicenseAcceptance,
                    p.Summary,
                    SupportedFrameworks = p.SupportedFrameworks.Select(pf => pf.TargetFramework).ToList(),
                    p.Tags,
                    p.Title,
                    p.Version,
                };

                foreach (var author in p.Authors)
                {
                    author.Package = null;
                }
                var jo = JObject.FromObject(pseudoPackage);
                string json = jo.ToString();
                ja.Add(jo);
            }

            File.WriteAllText(@"samplePackageData.json", ja.ToString());
        }
    }
}
