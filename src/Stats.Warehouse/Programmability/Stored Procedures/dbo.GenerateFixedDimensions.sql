CREATE PROCEDURE [dbo].[GenerateFixedDimensions]
AS

	-- ====================================================================
	-- Populates the Date dimension and inserts the unknown date row.
	-- ====================================================================

	DELETE [dbo].[Dimension_Date]

	IF NOT EXISTS(SELECT * FROM [dbo].[Dimension_Date] (NOLOCK) WHERE Id = -1)
	BEGIN
		SET IDENTITY_INSERT [dbo].[Dimension_Date] ON

		INSERT INTO [dbo].[Dimension_Date] (
		/*1*/   [Id]
				,[Date]
				,[DateName]
				,[DayOfWeek]
				,[DayOfWeekName]
				,[MonthName]
				,[WeekdayIndicator]
				,[DayOfYear]
				,[WeekOfYear]
		/*10*/	,[WeekOfYearName]
				,[WeekOfYearNameInYear]
				,[MonthOfYear]
				,[MonthOfYearName]
				,[MonthOfYearNameInYear]
				,[Quarter]
				,[QuarterName]
				,[QuarterNameInYear]
				,[HalfYear]
				,[HalfYearName]
		/*20*/	,[HalfYearNameInYear]
				,[Year]
				,[YearName]
				,[FiscalDayOfYear]
				,[FiscalWeekOfYear]
				,[FiscalWeekOfYearName]
				,[FiscalWeekOfYearNameInYear]
				,[FiscalMonthOfYear]
				,[FiscalMonthOfYearName]
				,[FiscalMonthOfYearNameInYear]
		/*30*/  ,[FiscalQuarter]
				,[FiscalQuarterName]
				,[FiscalQuarterNameInYear]
				,[FiscalHalfYear]
				,[FiscalHalfYearName]
				,[FiscalHalfYearNameInYear]
				,[FiscalYear]
		/*37*/	,[FiscalYearName])
			VALUES(
		/*1*/	-1
				,null
				,'(Unknown)'
				,null
				,'(Unknown)'
				,'(Unknown)'
				,'(Unknown)'
				,null
				,null
		/*10*/	,'(Unknown)'
				,'(Unknown)'
				,null
				,'(Unknown)'
				,'(Unknown)'
				,null
				,'(Unknown)'
				,'(Unknown)'
				,null
				,'(Unknown)'
		/*20*/	,'(Unknown)'
				,null
				,'(Unknown)'
				,null
				,null
				,'(Unknown)'
				,'(Unknown)'
				,null
				,'(Unknown)'
				,'(Unknown)'
		/*30*/	,null
				,'(Unknown)'
				,'(Unknown)'
				,null
				,'(Unknown)'
				,'(Unknown)'
				,null
		/*37*/	,'(Unknown)')

		SET IDENTITY_INSERT [dbo].[Dimension_Date] OFF

	END

	SET XACT_ABORT ON
	SET NOCOUNT ON
	DECLARE @Date DATE, @EndDate DATE
	SET @Date = CAST('2010-01-01' AS DATE)
	SET @EndDate = CAST('2099-12-31' AS DATE)

	IF (SELECT COUNT(*) FROM [dbo].[Dimension_Date] (NOLOCK) WHERE Id <> -1) = 0
	BEGIN

		DECLARE @FYDays INT, @FYWeek INT, @FYMonth INT, @FYQuarter INT, @FYYear INT, @FYStartDate DATETIME

		WHILE @Date <= @EndDate
		BEGIN
			SET @FYStartDate = '7/1/' +
				CASE WHEN DATEPART(MONTH, @date) < 7 THEN CAST(DATEPART(YEAR, @date) - 1 AS NVARCHAR(4))
				ELSE CAST(DATEPART(YEAR, @date) AS NVARCHAR(4))
			END
			SET @FYDays = DATEDIFF(DAY, @FYStartDate, @date) + 1
			SET @FYWeek = DATEDIFF(WEEK, @FYStartDate, @date) + 1
			SET @FYMonth = DATEDIFF(MONTH, @FYStartDate, @date) + 1
			SET @FYQuarter = CASE
				WHEN @FYMonth BETWEEN 1 AND 3 THEN 1
				WHEN @FYMonth BETWEEN 4 AND 6 THEN 2
				WHEN @FYMonth BETWEEN 7 AND 9 THEN 3
				WHEN @FYMonth BETWEEN 10 AND 12 THEN 4
			END
			SET @FYYear = DATEPART(YEAR, @FYStartDate) + 1

			INSERT INTO [dbo].[Dimension_Date] (
				[Date]
				,[DateName]
				,[DayOfWeek]
				,[DayOfWeekName]
				,[MonthName]
				,[WeekdayIndicator]
			-- CY
				,[DayOfYear]
				,[WeekOfYear]
				,[WeekOfYearName]
				,[WeekOfYearNameInYear]
				,[MonthOfYear]
				,[MonthOfYearName]
				,[MonthOfYearNameInYear]
				,[Quarter]
				,[QuarterName]
				,[QuarterNameInYear]
				,[HalfYear]
				,[HalfYearName]
				,[HalfYearNameInYear]
				,[Year]
				,[YearName]
			-- FY
				,[FiscalDayOfYear]
				,[FiscalWeekOfYear]
				,[FiscalWeekOfYearName]
				,[FiscalWeekOfYearNameInYear]
				,[FiscalMonthOfYear]
				,[FiscalMonthOfYearName]
				,[FiscalMonthOfYearNameInYear]
				,[FiscalQuarter]
				,[FiscalQuarterName]
				,[FiscalQuarterNameInYear]
				,[FiscalHalfYear]
				,[FiscalHalfYearName]
				,[FiscalHalfYearNameInYear]
				,[FiscalYear]
				,[FiscalYearName]
			)
			VALUES (
				@date
				,DATENAME(WEEKDAY, @date) + ', ' + DATENAME(MONTH, @date) + ' ' + DATENAME(DAY, @date) + ' ' + DATENAME(YEAR, @date)
				,DATEPART(WEEKDAY, @date)
				,DATENAME(WEEKDAY, @date)
				,DATENAME(month, @date)
				,CASE WHEN DATEPART(WEEKDAY, @date) > 1 AND DATEPART(WEEKDAY, @date) < 7 THEN 'Weekday' ELSE 'Weekend' END
			-- CY
				,DATEPART(DAYOFYEAR, @date)
				,DATEPART(WEEK, @date)
				,'Week ' + CAST(DATEPART(WEEK, @date) AS NVARCHAR(2))
				,'CY ' + DATENAME(YEAR, @date) + '-Week ' + CAST(DATEPART(WEEK, @date) AS NVARCHAR(2))
				,DATEPART(MONTH, @date)
				,'Month ' + CAST(DATEPART(MONTH, @date) as nvarchar(2))
				,'CY ' + DATENAME(YEAR, @date) + '-' + RIGHT(REPLICATE('0',2) + CAST(DATEPART(MONTH, @date) AS NVARCHAR(2)),2)
				,DATEPART(QUARTER, @date)
				,'Q' + CAST(DATEPART(QUARTER, @date) AS NVARCHAR(2))
				,'CY ' + DATENAME(YEAR, @date) + '-' + 'Q' + CAST(DATEPART(QUARTER, @date) AS NVARCHAR(1))
				,CASE WHEN DATEPART(MONTH, @date) < 7 THEN 1 ELSE 2 END
				,'H' + CAST(CASE WHEN DATEPART(MONTH, @date) < 7 THEN 1 ELSE 2 END AS NVARCHAR(1))
				,'CY ' + DATENAME(YEAR, @date) + '-' + 'H' + CAST(CASE WHEN DATEPART(MONTH, @date) < 7 THEN 1 ELSE 2 END AS NVARCHAR(1))
				,DATEPART(YEAR, @date)
				,'CY ' + DATENAME(YEAR, @date)
			-- FY
				,@FYDays
				,@FYWeek
				,'Week ' + CAST(@FYWeek AS NVARCHAR(2))
				,'FY ' + CAST(@FYYear AS NVARCHAR(4)) + '-Week ' + CAST(@FYWeek AS NVARCHAR(2))
				,@FYMonth
				,'Month ' + CAST(@FYMonth AS NVARCHAR(2))
				,'FY ' + CAST(@FYYear AS NVARCHAR(4)) + '-' + RIGHT(REPLICATE('0',2) + CAST(@FYMonth AS NVARCHAR(2)),2)
				,@FYQuarter
				,'Q' + CAST(@FYQuarter AS NVARCHAR(2))
				,'FY ' + CAST(@FYYear AS NVARCHAR(4)) + '-' + 'Q' + CAST(@FYQuarter AS NVARCHAR(2))
				,CASE WHEN @FYMonth < 7 THEN 1 ELSE 2 END
				,'H' + CAST(CASE WHEN @FYMonth < 7 THEN 1 ELSE 2 END AS NVARCHAR(1))
				,'FY ' + CAST(@FYYear AS NVARCHAR(4)) + '-' + 'H' + CAST(CASE WHEN @FYMonth < 7 THEN 1 ELSE 2 END AS NVARCHAR(1))
				,@FYYear
				,'FY ' + CAST(@FYYear AS NVARCHAR(4))
			)

			SET @Date = DATEADD(d, 1, @Date)
		END
	END

	-- ====================================================================
	-- Populates the Time dimension.
	-- ====================================================================

	DELETE [dbo].[Dimension_Time]

	DECLARE @current INT = 0;
	WHILE (@current < 24)
	BEGIN
		INSERT [dbo].[Dimension_Time] ( HourOfDay ) VALUES ( @current );
		SELECT @current = @current + 1;
	END

	-- ====================================================================
	-- Populates the Operation dimension.
	-- ====================================================================

	DELETE [dbo].[Dimension_Operation]

	SET IDENTITY_INSERT [dbo].[Dimension_Operation] ON

	INSERT [dbo].[Dimension_Operation] (Id, Operation) VALUES ( 1, '(unknown)' )

	SET IDENTITY_INSERT [dbo].[Dimension_Operation] OFF

	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Install' )
	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Update' )
	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Restore' )
	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Reinstall' )
	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Install-Dependency' )
	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Update-Dependency' )
	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Restore-Dependency' )
	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Reinstall-Dependency' )
	INSERT [dbo].[Dimension_Operation] (Operation) VALUES ( 'Mirror' )
	
	-- ====================================================================
	-- Populates the Client dimension.
	-- ====================================================================

	DELETE [dbo].[Dimension_Client]

	SET IDENTITY_INSERT [dbo].[Dimension_Client] ON
	INSERT [dbo].[Dimension_Client] (Id, ClientName, Major, Minor, Patch) VALUES ( 1, '(unknown)', 0, 0, 0 )
	SET IDENTITY_INSERT [dbo].[Dimension_Client] OFF

	-- ====================================================================
	-- Populates the ClientPlatform dimension.
	-- ====================================================================

	DELETE [dbo].[Dimension_Platform]
	SET IDENTITY_INSERT [dbo].[Dimension_Platform] ON
	INSERT [dbo].[Dimension_Platform] (Id, OSFamily, Major, Minor, Patch, PatchMinor) VALUES ( 1, '(unknown)', 0, 0, 0, 0 )
	SET IDENTITY_INSERT [dbo].[Dimension_Platform] OFF

	-- ====================================================================
	-- Populates the UserAgent facts.
	-- ====================================================================

	DELETE [dbo].[Fact_UserAgent]
	SET IDENTITY_INSERT [dbo].[Fact_UserAgent] ON
	INSERT [dbo].[Fact_UserAgent] (Id, UserAgent) VALUES ( 1, '(unknown)' )
	SET IDENTITY_INSERT [dbo].[Fact_UserAgent] OFF

	-- ====================================================================
	-- Populates the LogFileName facts.
	-- ====================================================================

	DELETE [dbo].[Fact_LogFileName]
	SET IDENTITY_INSERT [dbo].[Fact_LogFileName] ON
	INSERT [dbo].[Fact_LogFileName] (Id, LogFileName) VALUES ( 1, '(unknown)' )
	INSERT [dbo].[Fact_LogFileName] (Id, LogFileName) VALUES ( 2, '(manual correction)' )
	SET IDENTITY_INSERT [dbo].[Fact_LogFileName] OFF

RETURN 0
