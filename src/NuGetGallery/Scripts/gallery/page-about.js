$(function () {
    'use strict';

    // This script downloads information about our team members and external contributors and displays them in an inline grid.
    // It relies on the existence of a static json file containing a list of team members' GitHub aliases.
    // This list is then compared with contributors to the gallery and client on GitHub.
    // Contributors who are listed as team members are displayed as team members.
    // Contributors who are not listed by the static json file are displayed as external contributors.
    // Note that if a team member in the static json file has never contributed to the gallery or client GitHub repositories, they will not be shown here, regardless of whether or not they are listed.

    if (Promise == undefined) {
        // If promises aren't available this script will not work.
        // Promises are available in all but older, outdated browsers (such as IE 11).
        return;
    }

    // First, fetch the static json file of team members.
    $.getJSON(window.location.origin + '/api/v2/team', function (data) {
        var teamMap = {};

        // Parse it into a map that contains whether or not the contributor is a team member.
        $.each(data, function (index, member) {
            if (!teamMap.hasOwnProperty(member)) {
                teamMap[member] = 1;
            }
        });

        var repos = ['NuGetGallery', 'NuGet.Client'];
        var repoPromises = [];

        // Get the list of contributors from each repository.
        $.each(repos, function (index, repo) {
            repoPromises.push(getContributors(repo));
        });

        Promise.all(repoPromises)
            .then(function (contributorsPerRepo) {
                // A list of the team members.
                var team = [];
                // A list of the external contributors.
                var contributors = [];

                // This variable maps contributors to the number of contributions that they have made.
                // If contributors are in this map, they have been added to either the list of team members or the list of external contributors.
                var contributionsByContributorMap = {};

                // Iterate through the list of contributors for each repository, sorting each contributor by whether or not they are a NuGet team member and summing their contributions.
                $.each(contributorsPerRepo, function (index, repoContributors) {
                    $.each(repoContributors, function (index, repoContributor) {
                        if (!contributionsByContributorMap.hasOwnProperty(repoContributor.login)) {
                            // This contributor has not been added yet, so add them and set their total number of contributions to the number of contributions they have made to this repository.
                            if (teamMap.hasOwnProperty(repoContributor.login)) {
                                team.push(repoContributor);
                            } else {
                                contributors.push(repoContributor);
                            }
                            contributionsByContributorMap[repoContributor.login] = repoContributor.contributions;
                        } else {
                            // This contributor has already been added, but has made contributions to multiple repositories.
                            // Add the number of contributions they have made to this repository to their total number of contributions.
                            contributionsByContributorMap[repoContributor.login] = contributionsByContributorMap[repoContributor.login] + repoContributor.contributions;
                        }
                    });
                });

                // Sort the lists so that contributors with the most contributions appear first.
                function compareContributors(a, b) {
                    return contributionsByContributorMap[b.login] - contributionsByContributorMap[a.login];
                }

                team.sort(compareContributors);
                contributors.sort(compareContributors);

                ko.applyBindings({ team: team, contributors: contributors });

                // Animate the list so that it doesn't pop in.
                $(".contributors").hide().slideDown();
            });
    });

    // Fetches all of the contributors for a repo from GitHub.
    function getContributors(repo) {
        return getContributorsByPage(repo, 1, []);
    }

    function getContributorsByPage(repo, page, current) {
        // The list of contributors is paged so we must make multiple requests to get every contributor.
        return $.getJSON('https://api.github.com/repos/NuGet/' + repo + '/contributors?page=' + page)
            .then(function (repoContributors) {
                return repoContributors.length > 0 ?
                    getContributorsByPage(repo, page + 1, current.concat(repoContributors)) :
                    Promise.resolve(current);
            });
    }
});