CREATE PROCEDURE [dbo].[CompleteInvocation]
	@Id int,
	@RowVersion rowversion,
	@Status nvarchar(50),
	@Result nvarchar(50),
	@InstanceName nvarchar(100)
AS
	UPDATE	Invocations
	SET		[Complete] = 1,
			[Status] = @Status,
			[Result] = @Result,
			[LastInstanceName] = @InstanceName,
			[CompletedAt] = SYSDATETIMEOFFSET(),
			[LastUpdatedAt] = SYSDATETIMEOFFSET()
	WHERE	[Id] = @Id AND [RowVersion] = @RowVersion
