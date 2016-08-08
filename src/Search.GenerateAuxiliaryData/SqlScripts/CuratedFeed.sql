SELECT	[PackageRegistrations].[Id] 'Id',
		[CuratedFeeds].[Name] 'FeedName'
FROM	[PackageRegistrations] (NOLOCK)

INNER JOIN	[CuratedPackages] (NOLOCK)
ON			[CuratedPackages].[PackageRegistrationKey] = [PackageRegistrations].[Key]

INNER JOIN	[CuratedFeeds] (NOLOCK)
ON			[CuratedPackages].[CuratedFeedKey] = CuratedFeeds.[Key]