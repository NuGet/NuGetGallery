CREATE VIEW [dbo].[vwAggregatedDownloads] WITH SCHEMABINDING
	AS SELECT P.[PackageId]
	  ,P.[PackageVersion]
	  ,D.[Date]
	  ,T.[HourOfDay]
      ,SUM(ISNULL(F.[DownloadCount], 0)) AS TotalDownloadCount
	  ,C.[ClientName]
	  ,C.[ClientVersion]
	  ,O.[Operation]
	  ,PT.[ProjectType]
	  ,PL.[OSFamily]
	  ,PL.[OSVersion]
	  ,COUNT_BIG(*) AS [Count]
  FROM [dbo].[Fact_Download] AS F

  INNER JOIN	[dbo].[Dimension_Package] AS P
  ON			F.[Dimension_Package_Id] = P.[Id]
  INNER JOIN	[dbo].[Dimension_Date] AS D
  ON			F.[Dimension_Date_Id] = D.[Id]
  INNER JOIN	[dbo].[Dimension_Time] AS T
  ON			F.[Dimension_Time_Id] = T.[Id]
  INNER JOIN	[dbo].[Dimension_ProjectType] AS PT
  ON			F.[Dimension_ProjectType_Id] = PT.[Id]
  INNER JOIN	[dbo].[Dimension_Operation] AS O
  ON			F.[Dimension_Operation_Id] = O.[Id]
  INNER JOIN	[dbo].[Dimension_Client] AS C
  ON			F.[Dimension_Client_Id] = C.[Id]
  INNER JOIN	[dbo].[Dimension_Platform] AS PL
  ON			F.[Dimension_Platform_Id] = PL.[Id]

  GROUP BY	P.[PackageId],
			P.[PackageVersion],
			D.[Date],
			T.[HourOfDay],
			C.[ClientName],
			C.[ClientVersion],
			PL.[OSFamily],
			PL.[OSVersion],
			O.[Operation],
			PT.[ProjectType]