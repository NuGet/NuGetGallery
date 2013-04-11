# Deployment 2013.04.11  #

*Top 500 packages exposed in the feed*:

   The api/v2 feed now exposes the top downloaded packages (over the last 6 weeks).This can accessed be via url api/v2/stats/downloads. By default the top 500 packages would be shown.
   You can get the specific count of packages using ?count in the query string.
   Say, api/v2/stats/downloads?count=10 would return the top 10 downloaded packages in last 6 weeks - with information like download count, gallery url and feed url for that package.

*Numeric rank for packages stats*:

   The "Statistics" page now shows the numeric rank of the package (based on the download count) for the top 500 packages.

*Links to gravatar in profile page*:

   The edit profile page would now show a help text and link to gravatar making it easy for users to update their profile picture, if needed.

*UserName optimization in DB (backend)*:

   The "Users" table is optimized to have "UserName" as index for performance enhancements.

*Other minor bug fixes*:

   Complete list can be found here @ https://github.com/NuGet/NuGetGallery/issues?milestone=18
   


# Deployment 2013.03.28  #

*Support for MinClientVersion*:

   You can now upload packages with "[minclientVersion](http://nuget.codeplex.com/wikipage?title=NuGet%202.5%20list%20of%20features%20for%20Testing%20days%203%2f27%20to%203%2f29%20%2c%202013 )" to the NuGetGallery.
   The minclientVersion of the package will shown in the package home page right next to the package description.

*Contacting support*:

   The "Report Abuse" page has been revamped to enable users to chose the specific issue with the package they are reporting. It also guides the user to differentiate between "Contact Owners" and "Report Abuse".   

*Improvised Package statistics*:
   
   The package statistics now shows the break down of downloads based on the NuGet client (like NuGet CommandLine 2.1, NuGet Package Manager console 2.2 and so on and it also shows the split of the type of download operation (like Install, Restore, Update)

*Other minor bug fixes*:

   Complete list can be found here @ https://github.com/NuGet/NuGetGallery/issues?milestone=17


