// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.GitHubIndexer
{
    public class FetchedRepo : IFetchedRepo
    {
        public static FetchedRepo GetInstance(WritableRepositoryInformation repo, RepoUtils repoUtils, ILogger<FetchedRepo> logger)
        {
            var fetchedRepo = new FetchedRepo(repo, repoUtils, logger);
            fetchedRepo.Init();
            return fetchedRepo;
        }

        private readonly WritableRepositoryInformation _repoInfo;
        private readonly string _repoFolder;
        private readonly RepoUtils _repoUtils;
        private readonly ILogger<FetchedRepo> _logger;
        private Repository _repo;

        private FetchedRepo(WritableRepositoryInformation repoInfo, RepoUtils repoUtils, ILogger<FetchedRepo> logger)
        {
            _repoInfo = repoInfo ?? throw new ArgumentNullException(nameof(repoInfo));
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
            _repoFolder = Path.Combine(ReposIndexer.RepositoriesDirectory, repoInfo.Id);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private void Init()
        {
            CleanDirectory(new DirectoryInfo(_repoFolder));
            Repository.Init(_repoFolder);
            _repo = new Repository(_repoFolder);

            // Add the origin remote
            _repo.Network.Remotes.Add("origin", _repoInfo.Url);

            var remote = _repo.Network.Remotes["origin"];
            // Get the HEAD ref to only fetch the main branch
            var headRef = new string[] { "refs/heads/" + _repoInfo.MainBranch };

            _logger.LogInformation("[{RepoName}] Fetching branch {BranchName}.", _repoInfo.Id, _repoInfo.MainBranch);
            // Fetch
            Commands.Fetch(_repo, remote.Name, headRef, options: null, logMessage: "");
        }

        /// <summary>
        /// Persists the specified files to disk.
        /// </summary>
        /// <param name="filePaths">Paths to files in the repository</param>
        /// <returns>List of the persisted files on disk. The list is empty if filePaths is empty.</returns>
        public IReadOnlyList<ICheckedOutFile> CheckoutFiles(IReadOnlyCollection<string> filePaths)
        {
            if (!filePaths.Any())
            {
                return Array.Empty<ICheckedOutFile>();
            }

            _logger.LogInformation("[{RepoName}] Checking out {FileCount} files.", _repoInfo.Id, filePaths.Count);
            string mainBranchRef = "refs/remotes/origin/" + _repoInfo.MainBranch;
            _repo.CheckoutPaths(mainBranchRef, filePaths, new CheckoutOptions());

            var checkedOutFiles = new List<ICheckedOutFile>();
            foreach (var filePath in filePaths)
            {
                var fullPath = Path.Combine(_repoFolder, filePath);
                if (File.Exists(fullPath))
                {
                    checkedOutFiles.Add(new CheckedOutFile(fullPath, _repoInfo.Id));
                }
                else
                {
                    _logger.LogWarning(
                        "[{RepoName}] File was not checked out to disk: {FilePath}",
                        _repoInfo.Id,
                        filePath);
                }
            }

            return checkedOutFiles;
        }

        /// <summary>
        /// Cleans the repository folder and releases LibGit2Sharp hooks to the .git folder
        /// </summary>
        public void Dispose()
        {
            _logger.LogInformation("[{RepoName}] Cleaning repo folder.", _repoInfo.Id);
            _repo.Dispose();
            CleanDirectory(new DirectoryInfo(_repoFolder));
        }

        /// <summary>
        /// Returns a list of all files in the repository that are available in the repo's default branch.
        /// </summary>
        /// <returns>List of files in the repository</returns>
        public IReadOnlyList<GitFileInfo> GetFileInfos()
        {
            string mainBranchRef = "refs/remotes/origin/" + _repoInfo.MainBranch;
            var fileTree = _repo.Branches[mainBranchRef].Commits.ToList()[0].Tree;
            var basePathLength = Path.GetFullPath(_repoFolder).Length + 1; // Adding 1 for the separator size

            return _repoUtils
                .ListTree(fileTree, "", _repo)
                .Where(f =>
                    {
                        var isValid = (basePathLength + f.Path.Length) < 260;
                        if (!isValid)
                        {
                            _logger.LogWarning("[{RepoName}] Path too long for file {FileName}", _repoInfo.Id, f.Path);
                        }
                        return isValid;
                    })
                .ToList();
        }

        /// <summary>
        /// Recursively deletes all the files and sub-directories in a directory.
        /// Delegates to <see cref="DirectoryHelper"/> for robust handling of
        /// read-only files, NTFS race conditions, and Windows reserved device names.
        /// </summary>
        private void CleanDirectory(DirectoryInfo dir)
        {
            if (!dir.Exists)
            {
                return;
            }

            DirectoryHelper.DeleteDirectoryWithRetries(dir.FullName, _logger);
        }
    }
}
