CREATE PROCEDURE [dbo].[EnsureOperationDimensionsExist]
	@operations NVARCHAR(MAX) NULL
AS
BEGIN
	IF @operations IS NOT NULL
		BEGIN
			SET NOCOUNT ON;

			DECLARE @newDimensions TABLE(
				[Operation] NVARCHAR(32)
			)

			BEGIN TRY
				SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
				BEGIN TRANSACTION

					-- Check which dimensions are new
					INSERT INTO	@newDimensions ([Operation])
					SELECT		[Value]
					FROM		[dbo].[ParseCSVString](@operations)
					WHERE		[Value] NOT IN (SELECT [Operation] FROM [Dimension_Operation])

					-- Insert the new dimensions
					INSERT INTO	[Dimension_Operation]
					SELECT		[Operation]
					FROM		@newDimensions

					COMMIT

			END TRY
			BEGIN CATCH
				IF @@TRANCOUNT > 0
					ROLLBACK
			END CATCH

			-- Select all matching dimensions
			SELECT		[Id], [Operation]
			FROM		[Dimension_Operation]
			WHERE		ISNULL([Operation], '') IN (SELECT * FROM dbo.ParseCSVString(@operations))
		END
END