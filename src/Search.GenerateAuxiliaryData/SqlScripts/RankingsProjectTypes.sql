SELECT  TOP(@RankingCount)
        [Dimension_Package].[PackageId],
        SUM
        (
            CASE
                WHEN LOWER([Dimension_Operation].[Operation]) = 'install'
                THEN [Fact_Download].[DownloadCount]
                ELSE (0.5 * [Fact_Download].[DownloadCount])
            END
        ) 'Downloads'
FROM    [Fact_Download] (NOLOCK)

INNER JOIN  [Dimension_Package] (NOLOCK)
ON          [Dimension_Package].[Id] = [Fact_Download].[Dimension_Package_Id]

INNER JOIN  [Dimension_Date] (NOLOCK)
ON          [Dimension_Date].[Id] = [Fact_Download].[Dimension_Date_Id]

INNER JOIN  [Dimension_Operation] (NOLOCK)
ON          [Dimension_Operation].[Id] = [Fact_Download].[Dimension_Operation_Id]

INNER JOIN  [Fact_Download_Dimension_ProjectType] (NOLOCK)
ON          [Fact_Download_Dimension_ProjectType].[Fact_Download_Id] = [Fact_Download].[Id]

INNER JOIN  [Dimension_ProjectType] (NOLOCK)
ON          [Dimension_ProjectType].[Id] = [Fact_Download_Dimension_ProjectType].[Dimension_ProjectType_Id]

WHERE   [Dimension_Date].[Date] >= CONVERT(DATE, DATEADD(day, -42, GETUTCDATE()))
    AND [Dimension_Date].[Date] < CONVERT(DATE, GETUTCDATE())
    AND (
            LOWER([Dimension_Operation].[Operation]) = 'install'
            OR
            LOWER([Dimension_Operation].[Operation]) = 'update'
        )
    AND [Dimension_ProjectType].[ProjectType] = @ProjectGuid

GROUP BY    [Dimension_Package].[PackageId]
ORDER BY    Downloads DESC