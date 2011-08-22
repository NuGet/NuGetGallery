using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110821181500)]
    public class AddElmahSupportMigration : Migration {
        public override void Up() {
            Database.AddTable("ELMAH_Error", 
                new Column("ErrorId", DbType.Guid, ColumnProperty.NotNull, "NEWID()"),
                new Column("Application", DbType.StringFixedLength, 60, ColumnProperty.NotNull),
                new Column("Host", DbType.StringFixedLength, 50, ColumnProperty.NotNull),
                new Column("Type", DbType.StringFixedLength, 100, ColumnProperty.NotNull),
                new Column("Source", DbType.StringFixedLength, 60, ColumnProperty.NotNull),
                new Column("Message", DbType.StringFixedLength, 500, ColumnProperty.NotNull),
                new Column("[User]", DbType.StringFixedLength, 50, ColumnProperty.NotNull),
                new Column("StatusCode", DbType.Int32, ColumnProperty.NotNull),
                new Column("TimeUtc", DbType.DateTime, ColumnProperty.NotNull),
                new Column("Sequence", DbType.Int32, ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("AllXml", DbType.AnsiString, int.MaxValue, ColumnProperty.NotNull));
            Database.AddPrimaryKey("PK_ELMAH_Error", "ELMAH_Error", new[] { "ErrorId" });
            Database.ExecuteNonQuery(@"CREATE NONCLUSTERED INDEX IX_ELMAH_Error_App_Time_Seq ON ELMAH_Error (Application ASC, TimeUtc DESC, Sequence DESC)");            
            Database.ExecuteNonQuery(@"CREATE PROCEDURE ELMAH_GetErrorXml(@Application NVARCHAR(60), @ErrorId UNIQUEIDENTIFIER) AS SET NOCOUNT ON SELECT AllXml FROM ELMAH_Error WHERE ErrorId = @ErrorId AND Application = @Application");
            Database.ExecuteNonQuery(@"CREATE PROCEDURE ELMAH_GetErrorsXml(@Application NVARCHAR(60), @PageIndex INT = 0, @PageSize INT = 15, @TotalCount INT OUTPUT) AS SET NOCOUNT ON DECLARE @FirstTimeUTC DATETIME DECLARE @FirstSequence INT DECLARE @StartRow INT DECLARE @StartRowIndex INT SELECT @TotalCount = COUNT(1) FROM ELMAH_Error WHERE Application = @Application SET @StartRowIndex = @PageIndex * @PageSize + 1 IF @StartRowIndex <= @TotalCount BEGIN SET ROWCOUNT @StartRowIndex SELECT @FirstTimeUTC = TimeUtc, @FirstSequence = Sequence FROM ELMAH_Error WHERE Application = @Application ORDER BY TimeUtc DESC, Sequence DESC END ELSE BEGIN SET @PageSize = 0 END SET ROWCOUNT @PageSize SELECT errorId = ErrorId, application = Application, host = Host, type = Type, source = Source, message = Message, [user] = [User], statusCode = StatusCode, time = CONVERT(VARCHAR(50), TimeUtc, 126) + 'Z' FROM ELMAH_Error error WHERE Application = @Application AND TimeUtc <= @FirstTimeUTC AND Sequence <= @FirstSequence ORDER BY TimeUtc DESC, Sequence DESC FOR XML AUTO");            
            Database.ExecuteNonQuery(@"CREATE PROCEDURE ELMAH_LogError(@ErrorId UNIQUEIDENTIFIER, @Application NVARCHAR(60), @Host NVARCHAR(30), @Type NVARCHAR(100), @Source NVARCHAR(60), @Message NVARCHAR(500), @User NVARCHAR(50), @AllXml NTEXT, @StatusCode INT, @TimeUtc DATETIME) AS SET NOCOUNT ON INSERT INTO ELMAH_Error(ErrorId, Application, Host, Type, Source, Message, [User], AllXml, StatusCode, TimeUtc) VALUES (@ErrorId, @Application, @Host, @Type, @Source, @Message, @User, @AllXml, @StatusCode, @TimeUtc)");
        }

        public override void Down() {
            Database.ExecuteNonQuery("DROP PROCEDURE ELMAH_LogError");
            Database.ExecuteNonQuery("DROP PROCEDURE ELMAH_GetErrorsXml");
            Database.ExecuteNonQuery("DROP PROCEDURE ELMAH_GetErrorXml");
            Database.ExecuteNonQuery("DROP INDEX IX_ELMAH_Error_App_Time_Seq ON ELMAH_Error");
            Database.RemoveTable("ELMAH_Error");
        }
    }
}