CREATE PROCEDURE [dbo].[UpdateInvocation]
	@Id int,
	@RowVersion rowversion,
	@Status nvarchar(50),
	@Result nvarchar(50),
	@InstanceName nvarchar(100)
AS
	UPDATE	Invocations
	SET		[Status] = @Status,
			[Result] = @Result,
			[LastInstanceName] = @InstanceName,
			[LastUpdatedAt] = SYSDATETIMEOFFSET()
	WHERE	[Id] = @Id AND [RowVersion] = @RowVersion
