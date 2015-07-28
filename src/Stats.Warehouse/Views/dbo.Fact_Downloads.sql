CREATE VIEW [dbo].[Fact_Downloads]
	AS SELECT P.[PackageId]
	  ,P.[PackageVersion]
	  ,D.[Date]
	  ,T.[HourOfDay]
      ,SUM(F.[DownloadCount]) AS TotalDownloadCount
	  ,C.[ClientName]
	  ,CONCAT(ISNULL(C.[Major], '0'), '.', ISNULL(C.[Minor], '0'), '.', ISNULL(C.[Patch], '0')) AS ClientVersion
	  ,O.[Operation]
	  ,PT.[ProjectType]
	  ,PL.[OSFamily]
	  ,CONCAT(ISNULL(PL.[Major], '0'), '.', ISNULL(PL.[Minor], '0'), '.', ISNULL(PL.[Patch], '0'), '.', ISNULL(PL.[PatchMinor], '0')) AS OSVersion
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
			F.[Dimension_Client_Id],
			C.[ClientName],
			C.[Major],
			C.[Minor],
			C.[Patch],
			F.[Dimension_Platform_Id],
			PL.[OSFamily],
			PL.[Major],
			PL.[Minor],
			PL.[Patch],
			PL.[PatchMinor],
			O.[Operation],
			PT.[ProjectType]