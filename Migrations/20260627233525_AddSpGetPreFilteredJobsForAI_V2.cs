using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoWork.Migrations
{
    /// <inheritdoc />
    public partial class AddSpGetPreFilteredJobsForAI_V2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sp = @"
            CREATE PROCEDURE sp_GetPreFilteredJobs_ForAI_V2 
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

                -- 3️⃣ Get seeker skills
                ;WITH SeekerSkills AS (
                    SELECT ss.SkillId
                    FROM TbSeekerSkills ss
                    WHERE ss.SeekerId = @SeekerId
                )

                -- 4️⃣ Pre-ranked jobs (single category only)
                SELECT TOP 100
                    j.Id,
                    j.Title,
                    j.Description,
                    j.MinSalary,
                    j.MaxSalary,
                    j.PostedDate,
                    c.Name AS CategoryName,

                    STRING_AGG(s.Name, ', ') AS RequiredSkills,

                    (
                        50 +
                        (
                            SELECT COUNT(*)
                            FROM TbJobSkills js
                            INNER JOIN SeekerSkills ss ON ss.SkillId = js.SkillId
                            WHERE js.JobId = j.Id
                        ) * 15
                        +
                        CASE 
                            WHEN DATEDIFF(DAY, j.PostedDate, GETDATE()) <= 3 THEN 10
                            WHEN DATEDIFF(DAY, j.PostedDate, GETDATE()) <= 7 THEN 5
                            ELSE 0
                        END
                        +
                        CASE 
                            WHEN j.MaxSalary IS NOT NULL AND j.MaxSalary > 0 THEN 5
                            ELSE 0
                        END
                    ) AS PreRankScore

                FROM TbJobs j
                INNER JOIN TbCategories c ON j.CategoryId = c.Id
                LEFT JOIN TbJobSkills js ON j.Id = js.JobId
                LEFT JOIN TbSkills s ON js.SkillId = s.Id

                WHERE j.JobStatusId = 1
                  AND j.CategoryId = @InterestCategoryId

                GROUP BY 
                    j.Id, j.Title, j.Description, j.MinSalary, j.MaxSalary, j.PostedDate, c.Name

                ORDER BY PreRankScore DESC, j.PostedDate DESC;

            END";

            migrationBuilder.Sql(sp);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetPreFilteredJobs_ForAI_V2");
        }
    }
}
