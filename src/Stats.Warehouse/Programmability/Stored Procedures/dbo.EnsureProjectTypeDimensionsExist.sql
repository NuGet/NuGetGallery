CREATE PROCEDURE [dbo].[EnsureProjectTypeDimensionsExist]
	@projectTypes NVARCHAR(MAX) NULL
AS
BEGIN
	IF @projectTypes IS NOT NULL
		BEGIN
			SET NOCOUNT ON;

			DECLARE @newDimensions TABLE(
				[ProjectType] NVARCHAR(255)
			)

			BEGIN TRY

					-- Check which dimensions are new
					INSERT INTO	@newDimensions ([ProjectType])
					SELECT		[Value]
					FROM		[dbo].[ParseCSVString](@projectTypes)
					WHERE		[Value] NOT IN (SELECT [ProjectType] FROM [Dimension_ProjectType] (NOLOCK))

					SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
					BEGIN TRANSACTION

					-- Insert the new dimensions
					INSERT INTO	[Dimension_ProjectType]
					SELECT		[ProjectType]
					FROM		@newDimensions

					COMMIT

			END TRY
			BEGIN CATCH

				IF @@TRANCOUNT > 0
					ROLLBACK;

				THROW

			END CATCH

			-- Select all matching dimensions
			SELECT		[Id], [ProjectType]
			FROM		[Dimension_ProjectType] (NOLOCK)
			WHERE		ISNULL([ProjectType], '') IN (SELECT [Value] FROM dbo.ParseCSVString(@projectTypes))
		END
END