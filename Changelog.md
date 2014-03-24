#March 7, 2014

Fixed a bug that caused a limited number of users to see an error page when attempting to view the Manage My Packages page, as well as a couple other UI glitches. This release was coordinated with a new worker whose new jobs support our disaster recovery and failover plan. See [3.0.1](https://github.com/NuGet/NuGetGallery/issues?milestone=45&page=1&state=closed) for details.


#February 21, 2014

Changed the frequency of stats updates on the home page.  See [#1795](hhttps://github.com/NuGet/NuGetGallery/pull/1795) for details on the code change.


#February 6, 2014

Restored aggregate statistics to the home page, added an error message when package edits fail repeatedly, fixed a spurious error when uploading packages with dependencies that have no targetFramework assigned, and made a number of other fixes.  See [I7 - QA 1/6](https://github.com/NuGet/NuGetGallery/issues?milestone=39&page=1&state=closed) for details.

#January 2, 2014

Improved our detection algorithm for packages in the WebMatrix custom feed, included additional validation for package upload using the Gallery website, and made the experience for validation errors in the contact pages more consistent.  This iteration also included a number of other small user interface changes; see [I6 -12/06 QA - 01/02 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=38&page=1&state=closed) for details.

#Dec 6, 2013

Added support for Microsoft account login.  Also fixes a number of UI issues.  See [I5 -11/14 QA - 12/06 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=37&page=1&state=closed) for details.


#Nov 14, 2013

Bug fixes in a number of UI components, removing offensive terms, and removing the FriendlyLicenseNames configuration setting. Also fixes endless cycle of re-prompting for credentials in the client.  [I4 -11/1 QA - 11/14 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=36&page=1&state=closed)

#Nov 1,2013

Bug fixes in new user registration, stats, search indexing and "Manage my packages" page. Complete list can be found here: [I3 -10/18 QA - 11/1 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=35&page=1&state=closed)

#Oct 19,2013

### OAuth Phase 1

The back end changes to support OAuth in NuGet Gallery.

### Other bug fixes

Complete list can be found here: [10/04 - QA (10/18 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=34&page=1&state=closed)

#Oct 9,2013

### Friendly License Names

NuGet Gallery will now display the list of license names for a package in addition to simply providing a link to the license text. The big idea behind this feature is that it will aid in the decision making process over whether or not to use a package.
More details [here.](http://blog.nuget.org/20131011/friendly-license-names.html)

### Simplified user registration

New user registration workflow has been simplified with this deployment. Going forward, users don't have to confirm email for signing in. Email comfirmation can be done anytime before uploading a package.

### Normalized package versions

The package versions will be normalized in the package display page. For example, for a package with version "1.0", the version will be displayed as "1.0.0".
The normalized version will be displayed in the "Verify package details" page while uploading a new package. This avoids the issue where packages with version "1.1" and "1.1.0" co-exists in the Gallery.

### Other bug fixes

Other bug fixes in Glimpse integration and OData feed.Complete list can be found here: [09/20 - QA (10/09 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=33&state=closed)


#Sep 19, 2013

### Edit package and other bug fixes

A bunch of fixes around Edit package and expandable search box. Complete list can be found here: [09/6 - QA (09/19 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=32&page=1&state=closed)

#Sep 3, 2013

### Edit Package

You can now edit the metadata related to your NuGet package from the NuGet Gallery before and after uploading the package.
More details can be found [here](http://blog.nuget.org/20130823/Introducing-Edit-Package.html) in our team blog.

### Expandable Search box
The search box now gets auto-expanded whenever user tries to search for packages, making it easier to type in large search queries.

### Other bug fixes

Other changes include minor fixes in stats page, GetUpdates() API and updating NuGet.Core to 2.7-alpha.Complete list can be found here: [08/14 - QA (09/03 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=29)

#Aug 1, 2013

### Dependency update

The dependencies of nuget.org website like OData, NuGet.Core and Azure Storage have been updated to point to their latest versions respectively. 

### Other bug fixes

Other changes include minor fixes in stats page, GetUpdates() API and email validation for new user registration. Complete list can be found here: [07/19 - QA (08/02 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=27)

#July 19, 2013

### Nuget.org deployed on Azure Websites

The nuget.org website is now deployed on Azure web sites instead of Azure cloud services. Expect a detailed blog post from the NuGet team on the steps involed in migration and key take aways.
A couple of bug fixes were made to enable this migration( to be compatible with Azure web sites). 

- Canonical domain name for nuget.org : nuget.org will now re-direct to www.nuget.org.
 
- Lucence search index stored in Temp folder instead of AppData folder.
   
### Improved statistics

The [stats page](https://www.nuget.org/stats) now shows graphs for client usage and monthly download trends.
Also the stats for the individual packages now shows graphs for [downloads based on version](https://www.nuget.org/stats/packages/Newtonsoft.Json?groupby=Version) and "Install-Dependency" as an operation - which would help in indicating whether it is a direct install or install due to dependency.

### Other bug fixes

Other changes include [updated terms of use and privacy policy](https://www.nuget.org/policies/Privacy) and fixes to "Contact support" form. Complete list can be found here: [07/05 - QA (07/19 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=25&state=closed)

#July 8, 2013

### Accessiblity bug fixes

A bunch of accessiblity issue like sorting, highlighting and WCAG level A HTML 5 errors in the website are fixed.

### Other bug fixes

Other changes include code refactoring of the controllers for better testability, client side input validation for user registration and proper retrieval of tags from package file irrespective of the delimiter used. Complete list can be found here: [06/21 - QA (07/05 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=24&state=closed)

# June 21, 2013

### Filtering in GetUpdates() API based on target framework

The GetUpdates() in nuget.org API (V2) feed now allows filtering based on a specific target framework.

### Admin page bug fixes

A bunch of bug fixes related to the nuget.org Admin page (which shows up only for Administrator account) to modify and update database.

### Other bug fixes

Other bug fixes related to new user registration form and database schemna changes. Complete list can be found here: [06/10 - QA (06/20 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=23&state=closed)

# June 7, 2013

### Bug fixes in Search

Minor bug fixes in search to not show the version number of packages in search results and to support special characters in seeach queries. Now the search terms like "C++" ,"C#" should return precise results.

### Other bug fixes

Other bug fixes related to "Contact Support" form and unlisting packages. Complete list can be found here: [05/27 - QA (06/07 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=22&state=closed)

# May 23, 2013

### Remove unlisted packages from search index 

When a package gets unlisted, it will be removed from the Lucene search index immediately. This is one of the frequent ask from users as they don't want their unlisted packages to show up in search.

### Admin Page bug fixes

Bunch of fixes around the Admin page (which will be visible only for administrative login).

### Other minor bug fixes

Other minor fixes like client side validation for user name/email. Complete list can be found here: [05/13 - QA (05/24 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=21)

# May 13, 2013

### Front page enhancements

The contents for [nuget.org](nuget.org) home page is dynamically pulled from blob storage. This will help us to make announcements about new releases,
warnings or alerts about outages in an easy and quick way.

### Admin Page improvements

A new admin page is added to the nuget.org website which lets the administrators (core NuGet team members) to view error logs, rebuilding Lucenece index and similar admin actions.

### User created date

The user created date will be now be stored along with the user data in the dastabase. This enables getting statistics around users like registrations per week.

### Other minor bug fixes

Other minor fixes around statistics and curated feed.Complete list can be found here: [04/29 - QA (05/10 - Production)](https://github.com/NuGet/NuGetGallery/issues?milestone=20)


# April 25, 2013

### Group by Client Name,Version and Operation for download stats
The [package download statistics page] (//nuget.org/stats/) now allows you to group the download details based on package version, client version, client name and operation.

### WebMatrix curated feed performance improvements
The indexing of curated packages is optimized for performance so that search on a curated feed is on par with the search on regular feed. 

### Other minor bug fixes

Complete list can be found here: [Production Deployment 4/25](https://github.com/NuGet/NuGetGallery/issues?milestone=19)

# April 11, 2013

### Top 500 packages exposed in the feed

The nuget.org API (V2) feed now exposes the top downloaded packages (over the last 6 weeks). This can accessed be via url [nuget.org/api/v2/stats/downloads](//nuget.org/api/v2/stats/downloads). At this time, the top 500 packages are shown by default and that is also the maximum number returned.

You can limit the numbers of results using ?count in the query string.  For example, [nuget.org/api/v2/stats/downloads?count=10](//nuget.org/api/v2/stats/downloads?count=10) would return the top 10 downloaded packages in last 6 weeks - with information like download count, gallery url and feed url for that package.

The default and maximum count of 500 might change over time, so we recommend always specifying a count parameter if you are programmatically consuming this data.

### Numeric rank for packages stats

The [Statistics page](http://nuget.org/stats) now shows the numeric rank of the package (based on the download count).

### Links to gravatar in profile page

The profile editing page now includes help text and a link to gravatar making it easy for users to update their profile picture.

### UserName optimization in DB (backend)

The "Users" table is optimized to have "UserName" as index for performance enhancements.

### Other minor bug fixes

Complete list can be found here: [Production Deployment 4/12](https://github.com/NuGet/NuGetGallery/issues?milestone=18&state=closed)

# March 28, 2013

### Support for MinClientVersion

You can now upload packages with "[minclientVersion](http://nuget.codeplex.com/wikipage?title=NuGet%202.5%20list%20of%20features%20for%20Testing%20days%203%2f27%20to%203%2f29%20%2c%202013 )" to the NuGetGallery.

The minclientVersion of the package will shown in the package home page right next to the package description.

### Contacting support

The "Report Abuse" page has been revamped to enable users to chose the specific issue with the package they are reporting. It also guides the user to differentiate between "Contact Owners" and "Report Abuse".   

### Improved package statistics
   
The package statistics now shows the break down of downloads based on the NuGet client (like NuGet CommandLine 2.1, NuGet Package Manager console 2.2 and so on.  It also shows the split of the type of download operation (like Install, Restore, Update).

### Other minor bug fixes

Complete list can be found here: [Production Deployment 3/28](https://github.com/NuGet/NuGetGallery/issues?milestone=17&state=closed)


