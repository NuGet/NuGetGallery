namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageApproverUserKey : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "ApproverUserKey", c => c.Int());

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                  EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_ApproverUserKey]
                                          ON [dbo].[Packages] ([ApproverUserKey])
                                          WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_ApproverUserKey]
                      ON [dbo].[Packages] ([ApproverUserKey])
                  END");

            AddForeignKey("dbo.Packages", "ApproverUserKey", "dbo.Users", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Packages", "ApproverUserKey", "dbo.Users");
            DropIndex("dbo.Packages", new[] { "ApproverUserKey" });
            DropColumn("dbo.Packages", "ApproverUserKey");
        }
    }
}
