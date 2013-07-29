namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class UserLookupOptimization : DbMigration
    {
        public override void Up()
        {
            AlterColumn("Users", "EmailAddress", c => c.String(maxLength: 256));
            AlterColumn("Users", "UnconfirmedEmailAddress", c => c.String(maxLength: 256));
            AlterColumn("Users", "HashedPassword", c => c.String(maxLength: 256));
            AlterColumn("Users", "Username", c => c.String(nullable: false, maxLength: 64));
            AlterColumn("Users", "EmailConfirmationToken", c => c.String(maxLength: 32));
            AlterColumn("Users", "PasswordResetToken", c => c.String(maxLength: 32));

            // Index Users by Username
            CreateIndex("Users", "Username", unique: true, name: "IX_UsersByUsername");
        }

        public override void Down()
        {
            DropIndex("Users", "IX_UsersByUsername");

            AlterColumn("Users", "PasswordResetToken", c => c.String());
            AlterColumn("Users", "EmailConfirmationToken", c => c.String());
            AlterColumn("Users", "Username", c => c.String());
            AlterColumn("Users", "HashedPassword", c => c.String());
            AlterColumn("Users", "UnconfirmedEmailAddress", c => c.String());
            AlterColumn("Users", "EmailAddress", c => c.String());
        }
    }
}
