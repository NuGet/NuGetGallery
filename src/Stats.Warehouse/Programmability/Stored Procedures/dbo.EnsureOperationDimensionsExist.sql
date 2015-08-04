CREATE PROCEDURE [dbo].[EnsureOperationDimensionsExist]
	@operations NVARCHAR(MAX) NULL
AS
BEGIN
	IF @operations IS NOT NULL
		BEGIN
			SET NOCOUNT ON;

			DECLARE @newDimensions TABLE(
				[Operation] NVARCHAR(255)
			)

			BEGIN TRY
				SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
				BEGIN TRANSACTION

					-- Check which dimensions are new
					INSERT INTO	@newDimensions ([Operation])
					SELECT		[Value]
					FROM		[dbo].[ParseCSVString](@operations)
					WHERE		[Value] NOT IN (SELECT [Operation] FROM [Dimension_Operation] (NOLOCK))

					-- Insert the new dimensions
					INSERT INTO	[Dimension_Operation]
					SELECT		[Operation]
					FROM		@newDimensions

					COMMIT

			END TRY
			BEGIN CATCH

				IF @@TRANCOUNT > 0
					ROLLBACK;

				THROW

			END CATCH

			-- Select all matching dimensions
			SELECT		[Id], [Operation]
			FROM		[Dimension_Operation] (NOLOCK)
			WHERE		ISNULL([Operation], '') IN (SELECT [Value] FROM dbo.ParseCSVString(@operations))
		END
END