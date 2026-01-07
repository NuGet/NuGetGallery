CREATE TABLE [dbo].[Fact_UserAgent]
(
	[Id]                INT				IDENTITY (1, 1) NOT NULL,
    [UserAgent]         NVARCHAR(2048)	NULL
    CONSTRAINT [UserAgent] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF)
)
GO