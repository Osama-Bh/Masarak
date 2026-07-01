using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoWork.Migrations
{
    /// <inheritdoc />
    public partial class updatePrefilterdJobsSp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sp = @"
CREATE PROCEDURE sp_GetPreFilteredJobs_ForAI_Updated
    @SeekerId INT 
AS 
BEGIN 
    SET NOCOUNT ON; 

    -- 1️⃣ Validate seeker exists 
    IF NOT EXISTS (SELECT 1 FROM TbSeekers WHERE Id = @SeekerId) 
    BEGIN 
        RAISERROR ('Seeker not found.', 16, 1); 
        RETURN; 
    END 

    DECLARE @InterestCategoryId INT; 

    -- 2️⃣ Get seeker interest category 
    SELECT @InterestCategoryId = InterestCategoryId 
    FROM TbSeekers 
    WHERE Id = @SeekerId; 

    -- 3️⃣ Return pre-filtered jobs (Category match only) 
    SELECT
        j.Id, 
        j.Title, 
        j.Description, 
        j.MinSalary, 
        j.MaxSalary, 
        j.PostedDate, 
        c.Name AS CategoryName, 
        -- ✅ Aggregate required skills 
        STRING_AGG(s.Name, ', ') AS RequiredSkills 
    FROM TbJobs j 
    INNER JOIN TbCategories c ON j.CategoryId = c.Id 
    LEFT JOIN TbJobSkills js ON j.Id = js.JobId 
    LEFT JOIN TbSkills s ON js.SkillId = s.Id 
    WHERE j.JobStatusId = 1 -- Active jobs only 
      AND j.CategoryId = @InterestCategoryId 
    GROUP BY j.Id, j.Title, j.Description, j.MinSalary, j.MaxSalary, j.PostedDate, c.Name 
    ORDER BY j.PostedDate DESC; 
END";

            migrationBuilder.Sql(sp);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetPreFilteredJobs_ForAI_Updated");
        }
    }
}
