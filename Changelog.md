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


