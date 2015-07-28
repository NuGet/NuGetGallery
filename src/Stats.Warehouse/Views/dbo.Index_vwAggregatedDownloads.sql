CREATE UNIQUE CLUSTERED INDEX IDX_VIEW_vwAggregatedDownloads
ON [dbo].[vwAggregatedDownloads]
(
	[PackageId],
	[PackageVersion],
	[Date],
	[HourOfDay],
	[ClientName],
	[ClientVersion],
	[Operation],
	[ProjectType],
	[OSFamily],
	[OSVersion]
)