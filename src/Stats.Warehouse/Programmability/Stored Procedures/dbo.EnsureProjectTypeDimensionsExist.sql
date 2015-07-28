CREATE PROCEDURE [dbo].[EnsureProjectTypeDimensionsExist]
	@projectTypes NVARCHAR(MAX) NULL
AS
BEGIN
	IF @projectTypes IS NOT NULL
		BEGIN
			SET NOCOUNT ON;

			DECLARE @newDimensions TABLE(
				[ProjectType] NVARCHAR(32)
			)

			-- Check which dimensions are new
			INSERT INTO	@newDimensions ([ProjectType])
			SELECT		[Value]
			FROM		[dbo].[ParseCSVString](@projectTypes)
			WHERE		[Value] NOT IN (SELECT [ProjectType] FROM [Dimension_ProjectType])

			-- Insert the new dimensions
			INSERT INTO	[Dimension_ProjectType]
			SELECT		[ProjectType]
			FROM		@newDimensions

			-- Select all matching dimensions
			SELECT		[Id], [ProjectType]
			FROM		[Dimension_ProjectType]
			WHERE		[ProjectType] IN (SELECT * FROM dbo.ParseCSVString(@projectTypes))
		END
END