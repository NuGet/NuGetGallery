CREATE PROCEDURE [work].[EnqueueInvocation]
	@Job nvarchar(50),
	@Source nvarchar(50),
	@Payload nvarchar(MAX) = NULL,
	@NextVisibleAt datetime2,
	@InstanceName nvarchar(100),
    @JobInstanceName nvarchar(50) = NULL,
    @UnlessAlreadyRunning bit = 0
AS
    -- Insert a row with a completely new Id
	INSERT INTO [private].InvocationsStore(
            [Id],
            [Job],
            [Source],
            [Payload],
            [Status],
            [Result],
            [ResultMessage],
            [UpdatedBy],
            [LogUrl],
            [DequeueCount],
            [IsContinuation],
            [Complete],
            [LastDequeuedAt],
            [LastSuspendedAt],
            [CompletedAt],
            [QueuedAt],
            [NextVisibleAt],
            [UpdatedAt],
            [JobInstanceName])
	OUTPUT  inserted.*
    SELECT TOP 1
		    NEWID() AS [Id],
		    @Job AS [Job],
		    @Source AS [Source],
		    @Payload AS [Payload],
		    1 AS Status,
		    0 AS Result,
		    NULL AS ResultMessage,
		    @InstanceName AS UpdatedBy,
		    NULL AS LogUrl,
		    0 AS DequeueCount,
		    0 AS IsContinuation,
		    0 AS Complete,
		    NULL AS LastDequeuedAt,
		    NULL AS LastSuspendedAt,
		    NULL AS CompletedAt,
		    SYSUTCDATETIME() AS QueuedAt,
		    @NextVisibleAt AS NextVisibleAt,
		    SYSUTCDATETIME() AS UpdatedAt,
            @JobInstanceName AS JobInstanceName
    WHERE	(@UnlessAlreadyRunning = 0)
    OR		NOT EXISTS (
        SELECT Id, Job 
        FROM work.ActiveInvocations 
        WHERE Job = @Job 
        AND (@JobInstanceName = @JobInstanceName OR ((@JobInstanceName IS NULL) AND (@JobInstanceName IS NULL)))
        AND Result = 0
    )