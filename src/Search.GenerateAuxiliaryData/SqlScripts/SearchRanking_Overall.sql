SELECT	TOP(@RankingCount)
		[Dimension_Package].[PackageId],
		SUM
		(
			CASE
				WHEN LOWER([Dimension_Operation].[Operation]) = 'install'
				THEN [Fact_Download].[DownloadCount]
				ELSE (0.5 * [Fact_Download].[DownloadCount])
			END
		) 'Downloads'
FROM	[Fact_Download]

INNER JOIN	[Dimension_Package]
ON			[Dimension_Package].[Id] = [Fact_Download].[Dimension_Package_Id]

INNER JOIN	[Dimension_Date]
ON			[Dimension_Date].[Id] = [Fact_Download].[Dimension_Date_Id]

INNER JOIN	[Dimension_Operation]
ON			[Dimension_Operation].[Id] = [Fact_Download].[Dimension_Operation_Id]

WHERE	[Dimension_Date].[Date] >= CONVERT(DATE, DATEADD(day, -42, GETUTCDATE()))
	AND [Dimension_Date].[Date] < CONVERT(DATE, GETUTCDATE())
	AND (
			LOWER([Dimension_Operation].[Operation]) = 'install'
			OR
			LOWER([Dimension_Operation].[Operation]) = 'update'
		)

GROUP BY	[Dimension_Package].[PackageId]
ORDER BY	Downloads DESC