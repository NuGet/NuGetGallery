using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.GitHub
{
    public class GitHubCacheTransformer
    {
        public static Dictionary<string, NuGetPackageGitHubInformation> GetNuGetPackagesDependents(IReadOnlyList<RepositoryInformation> repositories)
        {
            var dependentsPerPackage = new Dictionary<string, List<RepositoryInformation>>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var repo in repositories)
            {
                foreach (var dependency in repo.Dependencies)
                {
                    List<RepositoryInformation> packageDependents = null;
                    if (dependentsPerPackage.ContainsKey(dependency))
                    {
                        packageDependents = dependentsPerPackage[dependency];
                    }
                    else
                    {
                        packageDependents = new List<RepositoryInformation>();
                    }
                    packageDependents.Add(repo);
                    dependentsPerPackage[dependency] = packageDependents;
                }
            }

            var gitHubUsageMap = new Dictionary<string, NuGetPackageGitHubInformation>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var entry in dependentsPerPackage)
            {
                entry.Value.Sort(Comparer<RepositoryInformation>.Create((x, y) =>
                {
                    var result = y.CompareTo(x); // Inverted for descending sort order
                    if (result != 0)
                    {
                        return result;
                    }

                    // Results have the same star count, compare their ids (not inverted) to sort in alphabetical order
                    return string.Compare(x.Id, y.Id, true);
                }));

                var nuGetPackageInformation = new NuGetPackageGitHubInformation();
                nuGetPackageInformation.TotalRepos = entry.Value.Count;
                nuGetPackageInformation.Repos = entry.Value.Take(10).ToList().AsReadOnly();
                gitHubUsageMap[entry.Key] = nuGetPackageInformation;
            }

            return gitHubUsageMap;
        }
    }
}
