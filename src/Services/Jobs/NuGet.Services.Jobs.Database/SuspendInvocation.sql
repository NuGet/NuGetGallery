CREATE PROCEDURE [dbo].[SuspendInvocation]
	@Id int,
	@RowVersion rowversion,
	@InstanceName nvarchar(100),
	@NextVisibleInMilliseconds int
AS
	UPDATE	Invocations
	SET		[Status] = 'Suspended',
			[Result] = 'Incomplete',
			[Dequeued] = 0,
			[LastInstanceName] = @InstanceName,
			[LastUpdatedAt] = SYSDATETIMEOFFSET(),
			[NextVisibleAt] = (DATEADD(ms, @NextVisibleInMilliseconds, SYSDATETIMEOFFSET()))
	WHERE	[Id] = @Id AND [RowVersion] = @RowVersion