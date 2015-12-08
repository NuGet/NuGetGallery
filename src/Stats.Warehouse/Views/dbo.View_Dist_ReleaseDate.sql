CREATE VIEW [dbo].[View_Dist_ReleaseDate]
	AS
	SELECT	Tool.[LowercasedToolId] AS 'Tool',
			Tool.[LowercasedToolVersion] AS 'Version',
			'IsPrerelease' =	CASE
									WHEN Tool.[LowercasedToolVersion] LIKE '%-%'
									THEN 1
									ELSE 0
								END,
			ISNULL(MIN(D.[Date]), '1900-01-01') AS 'StartDate',
			ISNULL(MIN(T.[HourOfDay]), 0) AS 'StartHour'
	FROM	[dbo].[Dimension_Tool] AS Tool (NOLOCK)

	INNER JOIN [dbo].[Fact_Dist_Download] AS F (NOLOCK)
	ON	F.[Dimension_Tool_Id] = Tool.[Id]

	INNER JOIN [dbo].[Dimension_Date] AS D (NOLOCK)
	ON	F.[Dimension_Date_Id] = D.[Id]

	INNER JOIN [dbo].[Dimension_Time] AS T (NOLOCK)
	ON	F.[Dimension_Time_Id] = T.[Id]

	WHERE	Tool.[LowercasedToolVersion] LIKE 'v%.%.%'

	GROUP BY	Tool.[LowercasedToolId],
				Tool.[LowercasedToolVersion]