namespace NuGet.Services.Validation
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddBatchIdToValidatorStatus : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ValidatorStatuses", "BatchId", c => c.String(maxLength: 20));
            CreateIndex("dbo.ValidatorStatuses", "BatchId", name: "IX_ValidatorStatuses_BatchId");
        }
        
        public override void Down()
        {
            DropIndex("dbo.ValidatorStatuses", "IX_ValidatorStatuses_BatchId");
            DropColumn("dbo.ValidatorStatuses", "BatchId");
        }
    }
}
