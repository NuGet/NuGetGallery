CREATE TABLE [dbo].[Dimension_Date]
(
    [Id]                          INT            IDENTITY (1, 1) NOT NULL,
    [Date]                        DATE           NULL,
    [DateName]                    NVARCHAR (150) NOT NULL,
    [DayOfWeek]                   INT            NULL,
    [DayOfWeekName]               NVARCHAR (30)  NOT NULL,
    [MonthName]                   NVARCHAR (30)  NOT NULL,
    [WeekdayIndicator]            NVARCHAR (10)  NOT NULL,
    [DayOfYear]                   INT            NULL,
    [WeekOfYear]                  INT            NULL,
    [WeekOfYearName]              NVARCHAR (10)  NOT NULL,
    [WeekOfYearNameInYear]        NVARCHAR (50)  NOT NULL,
    [MonthOfYear]                 INT            NULL,
    [MonthOfYearName]             NVARCHAR (10)  NOT NULL,
    [MonthOfYearNameInYear]       NVARCHAR (50)  NOT NULL,
    [Quarter]                     INT            NULL,
    [QuarterName]                 NVARCHAR (10)  NOT NULL,
    [QuarterNameInYear]           NVARCHAR (50)  NOT NULL,
    [HalfYear]                    INT            NULL,
    [HalfYearName]                NVARCHAR (10)  NOT NULL,
    [HalfYearNameInYear]          NVARCHAR (50)  NOT NULL,
    [Year]                        INT            NULL,
    [YearName]                    NVARCHAR (50)  NOT NULL,
    [FiscalDayOfYear]             INT            NULL,
    [FiscalWeekOfYear]            INT            NULL,
    [FiscalWeekOfYearName]        NVARCHAR (10)  NOT NULL,
    [FiscalWeekOfYearNameInYear]  NVARCHAR (20)  NOT NULL,
    [FiscalMonthOfYear]           INT            NULL,
    [FiscalMonthOfYearName]       NVARCHAR (10)  NOT NULL,
    [FiscalMonthOfYearNameInYear] NVARCHAR (10)  NOT NULL,
    [FiscalQuarter]               INT            NULL,
    [FiscalQuarterName]           NVARCHAR (10)  NOT NULL,
    [FiscalQuarterNameInYear]     NVARCHAR (20)  NOT NULL,
    [FiscalHalfYear]              INT            NULL,
    [FiscalHalfYearName]          NVARCHAR (10)  NOT NULL,
    [FiscalHalfYearNameInYear]    NVARCHAR (10)  NOT NULL,
    [FiscalYear]                  INT            NULL,
    [FiscalYearName]              NVARCHAR (10)  NOT NULL,
    CONSTRAINT [PK_Dimension_Date] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = ON)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Date_NCI_Date]
    ON [dbo].[Dimension_Date]([Date] ASC) WITH (STATISTICS_NORECOMPUTE = ON)
GO
CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Date_NCI_Date_Desc]
    ON [dbo].[Dimension_Date]([Date] DESC) WITH (STATISTICS_NORECOMPUTE = ON)
GO
CREATE NONCLUSTERED INDEX [Dimension_Date_NCI_WeekOfYear_Date] 
	ON [dbo].[Dimension_Date] ([WeekOfYear],[Year]) INCLUDE ([Date])
GO