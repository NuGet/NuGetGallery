SELECT PackageRegistrations.[Id] 'Id', CuratedFeeds.[Name] 'FeedName'
FROM PackageRegistrations
INNER JOIN CuratedPackages ON CuratedPackages.PackageRegistrationKey = PackageRegistrations.[Key]
INNER JOIN CuratedFeeds on CuratedPackages.CuratedFeedKey = CuratedFeeds.[Key]
