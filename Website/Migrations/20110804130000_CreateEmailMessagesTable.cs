using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations
{
    [Migration(20110804130000)]
    public class CreateEmailMessagesTableMigration : Migration
    {
        public override void Up()
        {
            Database.AddTable("EmailMessages",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("FromUserKey", DbType.Int32, ColumnProperty.Null),
                new Column("ToUserKey", DbType.Int32, ColumnProperty.NotNull),
                new Column("Subject", DbType.String, Const.MaxEmailSubjectLength, ColumnProperty.NotNull),
                new Column("Body", DbType.AnsiString, int.MaxValue, ColumnProperty.NotNull),
                new Column("Sent", DbType.Boolean, false));

            Database.AddForeignKey("FK_Messages_FromUser", "EmailMessages", "FromUserKey", "Users", "[Key]");
            Database.AddForeignKey("FK_Messages_ToUser", "EmailMessages", "ToUserKey", "Users", "[Key]");
        }

        public override void Down()
        {
            Database.RemoveTable("EmailMessages");
        }
    }
}