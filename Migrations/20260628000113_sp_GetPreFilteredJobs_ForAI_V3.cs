using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoWork.Migrations
{
    /// <inheritdoc />
    public partial class sp_GetPreFilteredJobs_ForAI_V3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sp = @"
CREATE PROCEDURE sp_GetPreFilteredJobs_ForAI_V3
    @SeekerId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate seeker exists
    IF NOT EXISTS (SELECT 1 FROM TbSeekers WHERE Id = @SeekerId)
    BEGIN
        RAISERROR ('Seeker not found.', 16, 1);
        RETURN;
    END;

    DECLARE @InterestCategoryId INT;

    -- Get seeker's interested category
    SELECT @InterestCategoryId = InterestCategoryId
    FROM TbSeekers
    WHERE Id = @SeekerId;

    ;WITH SeekerSkills AS
    (
        SELECT SkillId
        FROM TbSeekerSkills
        WHERE SeekerId = @SeekerId
    )

    SELECT TOP (30)
        j.Id,
        j.Title,
        j.Description,
        j.MinSalary,
        j.MaxSalary,
        j.PostedDate,
        c.Name AS CategoryName,
        STRING_AGG(s.Name, ', ') AS RequiredSkills,

        COUNT(DISTINCT CASE
            WHEN ss.SkillId IS NOT NULL THEN js.SkillId
        END) AS MatchingSkillCount

    FROM TbJobs j

    INNER JOIN TbCategories c
        ON c.Id = j.CategoryId

    LEFT JOIN TbJobSkills js
        ON js.JobId = j.Id

    LEFT JOIN TbSkills s
        ON s.Id = js.SkillId

    LEFT JOIN SeekerSkills ss
        ON ss.SkillId = js.SkillId

    WHERE j.JobStatusId = 1
      AND j.CategoryId = @InterestCategoryId

    GROUP BY
        j.Id,
        j.Title,
        j.Description,
        j.MinSalary,
        j.MaxSalary,
        j.PostedDate,
        c.Name

    HAVING COUNT(DISTINCT CASE
                WHEN ss.SkillId IS NOT NULL THEN js.SkillId
           END) > 0

    ORDER BY
        MatchingSkillCount DESC,
        j.PostedDate DESC;
END";

            migrationBuilder.Sql(sp);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetPreFilteredJobs_BySkillMatch");
        }
    }
}
