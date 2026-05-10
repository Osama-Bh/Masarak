using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.JobDTOs;
using GoWork.Enums;
using GoWork.Models;
using GoWork.Services.FileService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;
using Application = GoWork.Models.Application;

namespace GoWork.Services.JobService
{
    public class JobService : IJobService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IFileService _fileService;

        public JobService(ApplicationDbContext context, IConfiguration configuration, IFileService fileService)
        {
            _context = context;
            _configuration = configuration;
            _fileService = fileService;
        }

        // ==================== Job CRUD ====================

        public async Task<ApiResponse<PaginatedResult<JobListItemDTO>>> GetJobsAsync(
            int employerId, int page, int pageSize, string? search, string? status, int? jobTypeId)
        {
            await CheckAndExpireJobsAsync(employerId);

            var query = _context.TbJobs
                .Where(j => j.EmployerId == employerId)
                .Include(j => j.JobType)
                .Include(j => j.Currency)
                .Include(j => j.JobStatus)
                .Include(j => j.Applications)
                .AsQueryable();

            // Search by title
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(j => j.Title.Contains(search));
            }

            // Filter by status
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<JobStatusEnum>(status, true, out var statusEnum))
                {
                    query = query.Where(j => j.JobStatusId == (int)statusEnum);
                }
            }

            // Filter by job type
            if (jobTypeId.HasValue)
            {
                query = query.Where(j => j.JobTypeId == jobTypeId.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(j => j.PostedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new JobListItemDTO
                {
                    Id = j.Id,
                    Title = j.Title,
                    Description = j.Description,
                    JobType = j.JobType.Name,
                    MinSalary = j.MinSalary,
                    MaxSalary = j.MaxSalary,
                    Currency = j.Currency.Name,
                    PostedDate = j.PostedDate,
                    ExpirationDate = j.ExpirationDate,
                    ApplicantsCount = j.Applications != null ? j.Applications.Count : 0,
                    Status = j.JobStatus.Name
                })
                .ToListAsync();

            var result = new PaginatedResult<JobListItemDTO>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return new ApiResponse<PaginatedResult<JobListItemDTO>>(200, result);
        }

        public async Task<ApiResponse<JobDetailDTO>> GetJobByIdAsync(int employerId, int id)
        {
            await CheckAndExpireJobsAsync(employerId);

            var job = await _context.TbJobs
                .Where(j => j.Id == id && j.EmployerId == employerId)
                .Include(j => j.JobType)
                .Include(j => j.Category)
                .Include(j => j.JobLocationType)
                .Include(j => j.Currency)
                .Include(j => j.JobStatus)
                .Include(j => j.Address).ThenInclude(a => a.Country)
                .Include(j => j.Address).ThenInclude(a => a.Governate)
                .Include(j => j.JobSkills).ThenInclude(js => js.Skill)
                .Include(j => j.Applications)
                .FirstOrDefaultAsync();

            if (job == null)
                return new ApiResponse<JobDetailDTO>(404, "Job not found.");

            var detail = new JobDetailDTO
            {
                Id = job.Id,
                Title = job.Title,
                Description = job.Description,
                JobTypeId = job.JobTypeId,
                JobType = job.JobType.Name,
                CategoryId = job.CategoryId,
                Category = job.Category.Name,
                JobLocationTypeId = job.JobLocationTypeId,
                JobLocationType = job.JobLocationType.Name,
                CurrencyId = job.CurrencyId,
                Currency = job.Currency.Name,
                MinSalary = job.MinSalary,
                MaxSalary = job.MaxSalary,
                PostedDate = job.PostedDate,
                ExpirationDate = job.ExpirationDate,
                ApplicantsCount = job.Applications?.Count ?? 0,
                Status = job.JobStatus.Name,
                AddressLine = job.Address.AddressLine1,
                CountryId = job.Address.CountryId,
                Country = job.Address.Country.Name,
                GovernateId = job.Address.GovernateId,
                Governate = job.Address.Governate.Name,
                Skills = job.JobSkills?.Select(js => new SkillDTO
                {
                    Id = js.Skill.Id,
                    Name = js.Skill.Name
                }).ToList() ?? new()
            };

            return new ApiResponse<JobDetailDTO>(200, detail);
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> CreateJobAsync(int employerId, CreateJobDTO dto)
        {
            // Validate expiration date is in the future
            if (dto.ExpirationDate <= DateTime.UtcNow)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Expiration date must be in the future.");

            if (dto.MinSalary > dto.MaxSalary)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Min salary cannot be greater than max salary.");

            // Create address
            var address = new Address
            {
                AddressLine1 = dto.AddressLine ?? string.Empty,
                CountryId = dto.CountryId,
                GovernateId = dto.GovernateId
            };
            _context.TbAddresses.Add(address);
            await _context.SaveChangesAsync();

            // Create job
            var job = new Job
            {

                Title = dto.Title,
                Description = dto.Description,
                EmployerId = employerId,
                CategoryId = dto.CategoryId,
                JobTypeId = dto.JobTypeId,
                JobLocationTypeId = dto.JobLocationTypeId,
                AddressId = address.Id,
                MinSalary = dto.MinSalary,
                MaxSalary = dto.MaxSalary,
                CurrencyId = dto.CurrencyId,
                PostedDate = DateTime.UtcNow,
                ExpirationDate = dto.ExpirationDate,
                JobStatusId = (int)JobStatusEnum.Published
            };
            _context.TbJobs.Add(job);
            await _context.SaveChangesAsync();

            // Handle skills
            await HandleJobSkillsAsync(job.Id, dto.SkillIds, dto.NewSkills);

            return new ApiResponse<ConfirmationResponseDTO>(201, new ConfirmationResponseDTO
            {
                Message = "Job created successfully.",
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> UpdateJobAsync(int employerId, int id, UpdateJobDTO dto)
        {
            var job = await _context.TbJobs
                .Include(j => j.Address)
                .FirstOrDefaultAsync(j => j.Id == id && j.EmployerId == employerId);

            if (job == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Job not found.");

            if (dto.Title != null) job.Title = dto.Title;
            if (dto.Description != null) job.Description = dto.Description;
            if (dto.JobTypeId.HasValue) job.JobTypeId = dto.JobTypeId.Value;
            if (dto.CategoryId.HasValue) job.CategoryId = dto.CategoryId.Value;
            if (dto.JobLocationTypeId.HasValue) job.JobLocationTypeId = dto.JobLocationTypeId.Value;
            if (dto.CurrencyId.HasValue) job.CurrencyId = dto.CurrencyId.Value;
            if (dto.MinSalary.HasValue) job.MinSalary = dto.MinSalary.Value;
            if (dto.MaxSalary.HasValue) job.MaxSalary = dto.MaxSalary.Value;
            if (dto.ExpirationDate.HasValue)
            {
                job.ExpirationDate = dto.ExpirationDate.Value;

                // Auto-republish if the job was previously expired and the new date is in the future
                if (job.JobStatusId == (int)JobStatusEnum.Expired && job.ExpirationDate >= DateTime.UtcNow)
                {
                    job.JobStatusId = (int)JobStatusEnum.Published;
                }
            }

            // Update address if location fields provided
            if (dto.CountryId.HasValue || dto.GovernateId.HasValue || dto.AddressLine != null)
            {
                if (dto.CountryId.HasValue) job.Address.CountryId = dto.CountryId.Value;
                if (dto.GovernateId.HasValue) job.Address.GovernateId = dto.GovernateId.Value;
                if (dto.AddressLine != null) job.Address.AddressLine1 = dto.AddressLine;
            }

            // Update skills if provided
            if (dto.SkillIds != null || dto.NewSkills != null)
            {
                // Remove existing skills
                var existingSkills = await _context.TbJobSkills.Where(js => js.JobId == id).ToListAsync();
                _context.TbJobSkills.RemoveRange(existingSkills);
                await _context.SaveChangesAsync();

                await HandleJobSkillsAsync(id, dto.SkillIds ?? new(), dto.NewSkills ?? new());
            }

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Job updated successfully."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> UpdateJobStatusAsync(int employerId, int id, UpdateJobStatusDTO dto)
        {
            if (!Enum.TryParse<JobStatusEnum>(dto.Status, true, out var newStatus))
                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid status. Allowed: Published, Closed.");

            if (newStatus != JobStatusEnum.Published && newStatus != JobStatusEnum.Closed)
                return new ApiResponse<ConfirmationResponseDTO>(400, "You can only set status to Published or Closed.");

            var job = await _context.TbJobs.FirstOrDefaultAsync(j => j.Id == id && j.EmployerId == employerId);
            if (job == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Job not found.");

            if (newStatus == JobStatusEnum.Published && job.ExpirationDate < DateTime.UtcNow)
            {
                return new ApiResponse<ConfirmationResponseDTO>(400, "Cannot publish a job with an expiration date in the past. Please update the expiration date first.");
            }

            job.JobStatusId = (int)newStatus;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Job status updated successfully."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> DeleteJobAsync(int employerId, int id)
        {
            var job = await _context.TbJobs
                .Include(j => j.JobSkills)
                .Include(j => j.Applications)!.ThenInclude(a => a.Interviews)
                .FirstOrDefaultAsync(j => j.Id == id && j.EmployerId == employerId);

            if (job == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Job not found.");

            // Delete bottom-up: Interviews → Applications → JobSkills → Job
            if (job.Applications != null)
            {
                foreach (var app in job.Applications)
                {
                    if (app.Interviews != null)
                        _context.TbInterviews.RemoveRange(app.Interviews);
                }
                _context.TbApplications.RemoveRange(job.Applications);
            }

            if (job.JobSkills != null)
                _context.TbJobSkills.RemoveRange(job.JobSkills);

            _context.TbJobs.Remove(job);
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Job deleted successfully."
            });
        }

        // ==================== AI Recommendations ====================

        public async Task<ApiResponse<JobRecommendationResultDto>> GetJobRecommendationsAsync(int seekerId)
        {
            // 1. Fetch seeker and validate
            var seeker = await _context.TbSeekers
                .Include(s => s.InterestCategory)
                .Include(s => s.SeekerSkills).ThenInclude(ss => ss.Skill)
                .Include(s => s.Applications!).ThenInclude(a => a.Interviews)
                .FirstOrDefaultAsync(s => s.Id == seekerId);

            if (seeker == null)
            {
                return new ApiResponse<JobRecommendationResultDto>(404, "Seeker not found.");
            }

            var nameParts = new[] { seeker.FirsName, seeker.MiddleName, seeker.LastName }
                .Where(n => !string.IsNullOrWhiteSpace(n));
            
            var responseDto = new JobRecommendationResultDto
            {
                SeekerFullName = string.Join(" ", nameParts),
                TotalApplicationsCount = seeker.Applications?.Count ?? 0,
                PendingReviewApplicationsCount = seeker.Applications?.Count(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview) ?? 0,
                TotalInterviewsCount = seeker.Applications?.SelectMany(a => a.Interviews ?? Enumerable.Empty<Interview>()).Count() ?? 0
            };

            // 2. Fetch pre-filtered jobs via SP (up to 30 jobs for AI)
            var preFilteredJobs = await _context.Database.SqlQueryRaw<PreFilteredJobDTO>(
                "EXEC sp_GetPreFilteredJobs_ForAI @p0", seekerId)
                .AsNoTracking()
                .ToListAsync();

            if (!preFilteredJobs.Any())
            {
                return new ApiResponse<JobRecommendationResultDto>(200, responseDto);
            }

            // 3. Determine ranked order of job IDs
            List<int> rankedIds;

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Fallback: sort by posted date descending
                rankedIds = preFilteredJobs.OrderByDescending(j => j.PostedDate).Select(j => j.Id).ToList();
            }
            else
            {
                try
                {
                    // 4. Construct JSON objects for AI prompt
                    var candidateProfile = new
                    {
                        skills = seeker.SeekerSkills?.Select(ss => ss.Skill.Name).ToList() ?? new List<string>(),
                        category = seeker.InterestCategory?.Name ?? "General"
                    };

                    var jobsList = preFilteredJobs.Select(j => new
                    {
                        job_id = j.Id,
                        title = j.Title,
                        description = j.Description,
                        required_skills = string.IsNullOrWhiteSpace(j.RequiredSkills) ? new List<string>() : j.RequiredSkills.Split(',').Select(s => s.Trim()).ToList()
                    });

                    var candidateJson = JsonSerializer.Serialize(candidateProfile);
                    var jobsJson = JsonSerializer.Serialize(jobsList);

                    //var prompt = $@"
                    //You are a job ranking AI. Rank the following jobs for the candidate based on:
                    //1. Skills overlap (highest priority)
                    //2. Semantic similarity between job description and candidate skills

                    //Return ONLY valid JSON in this exact format:

                    //{{
                    //  ""ranked_jobs"": [
                    //    {{ ""job_id"": 10, ""score"": 0.92 }},
                    //    {{ ""job_id"": 11, ""score"": 0.81 }}
                    //  ]
                    //}}

                    //Do not include explanations or text outside JSON.

                    //Candidate:
                    //{candidateJson}

                    //Jobs:
                    //{jobsJson}";

                    var prompt = $@"
                    You are an expert AI recruiter. Your task is to evaluate and rank a list of jobs based on how well they match a candidate's profile.

                    Evaluation Criteria:
                    1. Validate each job's required skills and description against the candidate's skills and interest category.
                    2. Calculate a relevance score between 0.0 and 1.0 for each job (where 1.0 is a perfect match).
                    3. Rank the job IDs strictly from most relevant (highest score) to least relevant (lowest score).
                    4. You must include all provided jobs in the final ranking.

                    Return ONLY a valid JSON object in this exact format:
                    {{
                      ""ranked_jobs"": [
                        {{ ""job_id"": 10, ""score"": 0.95 }},
                        {{ ""job_id"": 11, ""score"": 0.82 }}
                      ]
                    }}

                    Do not include any explanations, markdown formatting, or text outside the JSON.

                    Candidate Data:
                    {candidateJson}

                    Jobs Data:
                    {jobsJson}";

                    // 5. Call OpenAI
                    var modelName = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
                    var chatClient = new ChatClient(modelName, apiKey);

                    var options = new ChatCompletionOptions
                    {
                        Temperature = 0.2f,
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
                    };

                    var completion = await chatClient.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(prompt) }, options);
                    var aiContent = completion.Value.Content[0].Text;

                    var resultDto = JsonSerializer.Deserialize<AIJobRankingResponseDTO>(aiContent);

                    if (resultDto?.RankedJobs != null && resultDto.RankedJobs.Any())
                    {
                        rankedIds = resultDto.RankedJobs
                            .OrderByDescending(r => r.Score)
                            .Select(r => r.JobId)
                            .ToList();
                    }
                    else
                    {
                        // AI returned nothing useful — fallback
                        rankedIds = preFilteredJobs.OrderByDescending(j => j.PostedDate).Select(j => j.Id).ToList();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AI Recommendation Failed: {ex.Message}");
                    rankedIds = preFilteredJobs.OrderByDescending(j => j.PostedDate).Select(j => j.Id).ToList();
                }
            }

            // 6. Enrich with full card data from EF
            var enriched = await _context.TbJobs
                .AsNoTracking()
                .Where(j => rankedIds.Contains(j.Id))
                .Select(j => new JobCardDto
                {
                    Id = j.Id,
                    Title = j.Title,
                    Description = j.Description,
                    CompanyName = j.Employer.ComapnyName,
                    CompanyLogoUrl = j.Employer.LogoUrl,
                    Category = j.Category.Name,
                    JobType = j.JobType.Name,
                    LocationType = j.JobLocationType.Name,
                    Country = j.Address != null && j.Address.Country != null ? j.Address.Country.Name : null,
                    Governate = j.Address != null && j.Address.Governate != null ? j.Address.Governate.Name : null,
                    MinSalary = j.MinSalary,
                    MaxSalary = j.MaxSalary,
                    PostedDate = j.PostedDate
                })
                .ToListAsync();

            // 7. Re-sort to preserve AI ranked order
            responseDto.Recommendations = rankedIds
                .Select(id => enriched.FirstOrDefault(j => j.Id == id))
                .Where(j => j != null)
                .Cast<JobCardDto>()
                .ToList();

            foreach (var recommendation in responseDto.Recommendations)
            {
                if (!string.IsNullOrWhiteSpace(recommendation.CompanyLogoUrl))
                {
                    recommendation.CompanyLogoUrl = _fileService.DownloadUrlAsync(recommendation.CompanyLogoUrl)?.SasUrl;
                }
            }

            return new ApiResponse<JobRecommendationResultDto>(200, responseDto);
        }


        //public async Task<ApiResponse<string>> EnhanceJobDescriptionAsync(EnhanceJobDescriptionDTO dto)
        //{
        //    var apiKey = _configuration["OpenAI:ApiKey"];
        //    if (string.IsNullOrWhiteSpace(apiKey))
        //    {
        //        return new ApiResponse<string>(500, "AI Service is not configured. Please contact support.");
        //    }

        //    try
        //    {
        //        var modelName = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        //        var chatClient = new ChatClient(modelName, apiKey);

        //        //var prompt = $@"
        //        //You are a professional HR and Technical Recruiter. Your goal is to take a draft job title and description and transform them into a high-quality, professional, and engaging job posting. 

        //        //Instructions:
        //        //1. Improve the structure and flow of the content.
        //        //2. Use professional and persuasive language to attract top talent.
        //        //3. Organize the information into clear sections such as 'About the Role', 'Responsibilities', and 'Requirements'.
        //        //4. Ensure the tone is appropriate for a modern workplace.
        //        //5. Do NOT change the core meaning or the essential requirements of the job.
        //        //6. Return ONLY the enhanced description text, without any additional comments, markdown headers like '###', or conversational filler.
        //        //7. If the input is in Arabic, respond in Arabic. If English, respond in English.

        //        //Job Title: {dto.Title}
        //        //Original Description: {dto.Description}";

        //        var messages = new List<ChatMessage>
        //        {
        //            new SystemChatMessage("You are a professional HR and Technical Recruiter. Your task is to enhance job descriptions to be more professional, engaging, and structured. Use clear sections like 'About the Role', 'Responsibilities', and 'Requirements'. Do not include markdown headers like '###' or any conversational filler. Return ONLY the enhanced description. Respond in the same language as the input (Arabic or English)."),
        //            new UserChatMessage($"Job Title: {dto.Title}\n\nDraft Description:\n{dto.Description}")
        //        };

        //        var options = new ChatCompletionOptions
        //        {
        //            Temperature = 0.3f,
        //        };

        //        //var completion = await chatClient.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(prompt) }, options);
        //        //var enhancedDescription = completion.Value.Content[0].Text?.Trim();

        //        var completion = await chatClient.CompleteChatAsync(messages, options);

        //        // Get the first content part that is text
        //        var enhancedDescription = completion.Value.Content.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Text))?.Text?.Trim();


        //        if (string.IsNullOrWhiteSpace(enhancedDescription))
        //        {
        //            return new ApiResponse<string>(500, "AI failed to generate an enhanced description.");
        //        }

        //        return new ApiResponse<string>(200, enhancedDescription);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"AI Enhancement Failed: {ex.Message}");
        //        return new ApiResponse<string>(500, "An error occurred while communicating with the AI service.");
        //    }
        //}


        // ==================== Job Applications ====================

        public async Task<ApiResponse<JobDescriptionEnhancementResultDTO>> EnhanceJobDescriptionAsync(EnhanceJobDescriptionDTO dto)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new ApiResponse<JobDescriptionEnhancementResultDTO>(500, "AI Service is not configured. Please contact support.");
            }

            try
            {
                var modelName = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
                var chatClient = new ChatClient(modelName, apiKey);

                var prompt = $@"
                You are a professional HR and Technical Recruiter. Your task is to take a draft job title and description and transform them into a high-quality, professional, and engaging job posting. 

                Instructions:
                1. Improve the structure and flow of the content.
                2. Use professional and persuasive language to attract top talent.
                3. Organize the information into clear sections such as 'About the Role', 'Responsibilities', and 'Requirements'.
                4. Ensure the tone is appropriate for a modern workplace.
                5. Do NOT change the core meaning or the essential requirements of the job.
                6. If the input is in Arabic, respond in Arabic. If English, respond in English.
                7. CRITICAL: The total length of the enhanced description MUST be under 700 characters to fit the system's constraints.

                Return ONLY a valid JSON object in this exact format:
                {{
                  ""enhanced_description"": ""your_enhanced_text_here""
                }}

                Do not include any explanations, markdown formatting, or text outside the JSON.

                Job Title: {dto.Title}
                Original Description: {dto.Description}";

                

                var options = new ChatCompletionOptions
                {
                    Temperature = 0.3f,
                    ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
                };

                var completion = await chatClient.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(prompt) }, options);

                if (completion == null || completion.Value == null || completion.Value.Content == null || completion.Value.Content.Count == 0)
                {
                    return new ApiResponse<JobDescriptionEnhancementResultDTO>(500, "AI failed to generate an enhanced description.");
                }

                var aiContent = completion.Value.Content[0].Text;
                var resultDto = JsonSerializer.Deserialize<JobDescriptionEnhancementResultDTO>(aiContent);
                
                if (resultDto == null || string.IsNullOrWhiteSpace(resultDto.EnhancedDescription))
                {
                    return new ApiResponse<JobDescriptionEnhancementResultDTO>(500, "AI returned an empty response.");
                }

                return new ApiResponse<JobDescriptionEnhancementResultDTO>(200, resultDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Enhancement Failed: {ex.Message}");
                return new ApiResponse<JobDescriptionEnhancementResultDTO>(500, "An error occurred while communicating with the AI service.");
            }
        }

        //private async Task<int> CalculateMatchScoreAsync(Seeker seeker, Job job)
        //{
        //    var apiKey = _configuration["OpenAI:ApiKey"];
        //    if (string.IsNullOrWhiteSpace(apiKey)) return 10; // Default to pass if AI not configured

        //    try
        //    {
        //        var candidateProfile = new
        //        {
        //            skills = seeker.SeekerSkills?.Select(ss => ss.Skill.Name).ToList() ?? new List<string>(),
        //            major = seeker.Major ?? "Not specified"
        //        };

        //        var jobRequirements = new
        //        {
        //            title = job.Title,
        //            description = job.Description,
        //            required_skills = job.JobSkills?.Select(js => js.Skill.Name).ToList() ?? new List<string>()
        //        };

        //        var prompt = $@"
        //        You are an expert AI recruiter. Evaluate the matching between the candidate and the job requirements.
        //        Candidate Data: {JsonSerializer.Serialize(candidateProfile)}
        //        Job Data: {JsonSerializer.Serialize(jobRequirements)}

        //        Instructions:
        //        1. Provide a match score from 1 to 10 (integer).
        //        2. Return ONLY a valid JSON object with a 'score' field, without any explanations or additional text, in this exact format:
        //        {{ ""score"": 7 }}
        //        3. Do not include any explanations, markdown formatting, or text outside the JSON.";

        //        var modelName = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        //        var chatClient = new ChatClient(modelName, apiKey);

        //        var options = new ChatCompletionOptions
        //        {
        //            Temperature = 0.2f,
        //            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        //        };

        //        var completion = await chatClient.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(prompt) }, options);
        //        var aiContent = completion.Value.Content[0].Text;

        //        using var doc = JsonDocument.Parse(aiContent);
        //        if (doc.RootElement.TryGetProperty("score", out var scoreElement))
        //        {
        //            return scoreElement.GetInt32();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"AI Matching Failed: {ex.Message}");
        //    }

        //    return 10; // Fallback to pass to avoid blocking users on technical errors
        //}


        private async Task<int?> CalculateMatchingPercentageAsync(int seekerId, int jobId)
        {
            try
            {
                var seeker = await _context.TbSeekers
                    .Include(s => s.SeekerSkills).ThenInclude(ss => ss.Skill)
                    .FirstOrDefaultAsync(s => s.Id == seekerId);

                var job = await _context.TbJobs
                    .Include(j => j.JobSkills).ThenInclude(js => js.Skill)
                    .FirstOrDefaultAsync(j => j.Id == jobId);

                if (seeker == null || job == null) return null;

                var seekerSkills = string.Join(", ", seeker.SeekerSkills.Select(s => s.Skill.Name));
                var jobSkills = string.Join(", ", job.JobSkills.Select(s => s.Skill.Name));

                // Get CV SAS URL
                string cvUrl = "No CV provided";
                if (!string.IsNullOrWhiteSpace(seeker.ResumeUrl))
                {
                    cvUrl = _fileService.DownloadUrlAsync(seeker.ResumeUrl)?.SasUrl ?? "No CV provided";
                }

                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey)) return null;

                var modelName = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
                var chatClient = new ChatClient(modelName, apiKey);

                var prompt = $@"
                    You are an expert technical recruiter and HR specialist. Your task is to calculate a match percentage (0-100) between a candidate and a job posting.
                    
                    
                    ### Candidate Information:
                    - **Skills**: {seekerSkills}
                    - **Resume/CV URL**: {cvUrl}
                    
                    ### Job Requirements:
                    - **Title**: {job.Title}
                    - **Description**: {job.Description}
                    - **Required Skills**: {jobSkills}
                    
                    ### Instructions:
                    1. Evaluate the candidate's alignment with the job based on their major, listed skills, and the job description.
                    2. Consider the provided Resume/CV URL as a primary source of information for the candidate's background.
                    3. Return ONLY a single integer between 0 and 100 representing the match percentage.
                    4. Do not include any text, symbols, or explanations.
                    5. Return ONLY a valid JSON object with a 'Percentage' field, without any explanations or additional text, in this exact format:
                {{{{ """"Percentage"""": 70 }}}}
                    6. Do not include any explanations, markdown formatting, or text outside the JSON."";
                    
                ";

                var options = new ChatCompletionOptions { Temperature = 0.1f };
                var completion = await chatClient.CompleteChatAsync(new ChatMessage[] { new UserChatMessage(prompt) }, options);
                var aiResponse = completion.Value.Content[0].Text?.Trim();

                if (int.TryParse(aiResponse, out int percentage))
                {
                    return percentage;
                }

                return null;
            }
            catch (Exception ex)
            {
                return 500; // Return a larger value to avoid false negatives in matching if AI fails, since this is a non-critical enhancement
            }
        }

        public async Task<ApiResponse<ApplicationResultDto>> ApplyToJobAsync(int jobId, int seekerId)
        {
            var job = await _context.TbJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId);

            //var job = await _context.TbJobs
            //    .Include(j => j.JobSkills).ThenInclude(js => js.Skill)
            //    .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
            {
                return new ApiResponse<ApplicationResultDto>(404, "Job not found.");
            }

            if (job.JobStatusId != (int)JobStatusEnum.Published || job.ExpirationDate < DateTime.UtcNow)
            {
                return new ApiResponse<ApplicationResultDto>(400, "Job is closed or expired.");
            }

            var seekerExists = await _context.TbSeekers.AnyAsync(s => s.Id == seekerId);
            if (!seekerExists)
            {
                return new ApiResponse<ApplicationResultDto>(404, "Candidate not found.");
            }

            //var seeker = await _context.TbSeekers
            //   .Include(s => s.SeekerSkills).ThenInclude(ss => ss.Skill)
            //   .FirstOrDefaultAsync(s => s.Id == seekerId);

            //if (seeker == null)
            //{
            //    return new ApiResponse<ApplicationResultDto>(404, "Candidate not found.");
            //}

            var alreadyApplied = await _context.TbApplications.AnyAsync(a => a.JobId == jobId && a.SeekerId == seekerId);
            if (alreadyApplied)
            {
                return new ApiResponse<ApplicationResultDto>(400, "You have already applied for this job.");
            }

            //var score = await CalculateMatchScoreAsync(seeker, job);
            //var statusId = (int)ApplicationStatusEnum.PendingReview;
            //var message = "Application submitted successfully.";

            //if (score < 5)
            //{
            //    statusId = (int)ApplicationStatusEnum.Rejected;
            //    message = "Application submitted, but did not meet the matching criteria for this role.";
            //}

            var application = new Application
            {
                JobId = jobId,
                SeekerId = seekerId,
                ApplicationDate = DateTime.UtcNow,
                ApplicationStatusId = (int)ApplicationStatusEnum.PendingReview,
                MatchingPercentage = await CalculateMatchingPercentageAsync(seekerId, jobId)
                //ApplicationStatusId = statusId
            };

            _context.TbApplications.Add(application);
            await _context.SaveChangesAsync();

            var result = new ApplicationResultDto
            {
                ApplicationId = application.Id,
                Message = "Application submitted successfully."
                //Message = message
            };

            return new ApiResponse<ApplicationResultDto>(200, result);
        }

        // ==================== Job Details ====================

        public async Task<ApiResponse<JobDetailsDto>> GetJobDetailsAsync(int jobId, int? seekerId)
        {
            var jobInfo = await _context.TbJobs
                .AsNoTracking()
                .Where(j => j.Id == jobId)
                .Select(j => new
                {
                    Details = new JobDetailsDto
                    {
                        Id = j.Id,
                        Title = j.Title,
                        Description = j.Description,
                        Category = j.Category.Name,
                        JobType = j.JobType.Name,
                        JobLocationType = j.JobLocationType.Name,
                        MinSalary = j.MinSalary,
                        MaxSalary = j.MaxSalary,
                        Currency = j.Currency.Name,
                        PostedDate = j.PostedDate,
                        ExpirationDate = j.ExpirationDate,
                        Country = j.Address != null && j.Address.Country != null ? j.Address.Country.Name : null,
                        Skills = j.JobSkills.Select(js => js.Skill.Name).ToList(),
                        CanApply = seekerId.HasValue && !j.Applications.Any(a => a.SeekerId == seekerId.Value),
                        Company = new JobDetailsCompanyDto
                        {
                            Name = j.Employer.ComapnyName,
                            LogoUrl = j.Employer.LogoUrl
                        }
                    },
                    JobStatusId = j.JobStatusId
                })
                .FirstOrDefaultAsync();

            if (jobInfo == null)
            {
                return new ApiResponse<JobDetailsDto>(404, "Job not found.");
            }

            if (jobInfo.JobStatusId != (int)JobStatusEnum.Published)
            {
                return new ApiResponse<JobDetailsDto>(410, "Job is no longer available.");
            }

            if (jobInfo.Details != null && jobInfo.Details.Company != null && !string.IsNullOrWhiteSpace(jobInfo.Details.Company.LogoUrl))
            {
                jobInfo.Details.Company.LogoUrl = _fileService.DownloadUrlAsync(jobInfo.Details.Company.LogoUrl)?.SasUrl;
            }

            return new ApiResponse<JobDetailsDto>(200, jobInfo.Details);
        }

        // ==================== Job Search ====================

        public async Task<ApiResponse<JobSearchResponseDto>> SearchJobsAsync(JobSearchRequestDto request)
        {
            var query = _context.TbJobs.AsNoTracking().Where(j => j.JobStatusId == (int)JobStatusEnum.Published);

            bool noFilters = string.IsNullOrWhiteSpace(request.Search) &&
                             request.CategoryId == null &&
                             request.JobTypeId == null &&
                             request.JobLocationTypeId == null &&
                             request.CountryId == null;

            //if (noFilters && request.SeekerId.HasValue)
            //{
            //    var seeker = await _context.TbSeekers.FirstOrDefaultAsync(s => s.Id == request.SeekerId.Value);
            //    if (seeker != null)
            //    {
            //        query = query.Where(j => j.CategoryId == seeker.InterestCategoryId);
            //    }
            //}
            
            // Apply text search
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var searchLower = request.Search.ToLower();
                query = query.Where(j => j.Title.ToLower().Contains(searchLower) || j.Description.ToLower().Contains(searchLower));
            }

            // Apply strict filters
            if (request.CategoryId.HasValue)
                query = query.Where(j => j.CategoryId == request.CategoryId.Value);

            if (request.JobTypeId.HasValue)
                query = query.Where(j => j.JobTypeId == request.JobTypeId.Value);

            if (request.JobLocationTypeId.HasValue)
                query = query.Where(j => j.JobLocationTypeId == request.JobLocationTypeId.Value);

            if (request.CountryId.HasValue)
                query = query.Where(j => j.Address != null && j.Address.CountryId == request.CountryId.Value);

            // Apply sorting
            bool isDesc = string.Equals(request.SortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? false : true;
            var sortBy = request.SortBy?.ToLower() ?? "date";

            switch (sortBy)
            {
                case "salary":
                    query = isDesc ? query.OrderByDescending(j => j.MaxSalary) : query.OrderBy(j => j.MaxSalary);
                    break;
                case "title":
                    query = isDesc ? query.OrderByDescending(j => j.Title) : query.OrderBy(j => j.Title);
                    break;
                case "date":
                default:
                    query = isDesc ? query.OrderByDescending(j => j.PostedDate) : query.OrderBy(j => j.PostedDate);
                    break;
            }

            // Apply Pagination
            if (request.Page < 1) request.Page = 1;
            if (request.PageSize < 1 || request.PageSize > 50) request.PageSize = 30;

            int totalCount = await query.CountAsync();
            
            var pagedJobs = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(j => new JobCardDto
                {
                    Id = j.Id,
                    Title = j.Title,
                    Description = j.Description,
                    CompanyName = j.Employer.ComapnyName,
                    CompanyLogoUrl = j.Employer.LogoUrl,
                    Category = j.Category.Name,
                    JobType = j.JobType.Name,
                    LocationType = j.JobLocationType.Name,
                    Country = j.Address != null && j.Address.Country != null ? j.Address.Country.Name : null,
                    Governate = j.Address != null && j.Address.Governate != null ? j.Address.Governate.Name : null,
                    MinSalary = j.MinSalary,
                    MaxSalary = j.MaxSalary,
                    PostedDate = j.PostedDate
                })
                .ToListAsync();

            foreach (var job in pagedJobs)
            {
                if (!string.IsNullOrWhiteSpace(job.CompanyLogoUrl))
                {
                    job.CompanyLogoUrl = _fileService.DownloadUrlAsync(job.CompanyLogoUrl)?.SasUrl;
                }
            }

            return new ApiResponse<JobSearchResponseDto>(200, new JobSearchResponseDto
            {
                Jobs = pagedJobs,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                HasNextPage = (request.Page * request.PageSize) < totalCount
            });
        }

        // ==================== Lookups ====================

        public async Task<ApiResponse<List<LookupDTO>>> GetCategoriesAsync(string? search)
        {
            var query = _context.TbCategories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Name.Contains(search));
            }

            var items = await query
                .OrderBy(c => c.Name)
                .Select(c => new LookupDTO { Id = c.Id, Name = c.Name })
                .ToListAsync();
            return new ApiResponse<List<LookupDTO>>(200, items);
        }

        public async Task<ApiResponse<List<LookupDTO>>> GetJobTypesAsync()
        {
            var items = await _context.TbJobTypes
                .Where(jt => jt.IsActive)
                .OrderBy(jt => jt.SortOrder)
                .Select(jt => new LookupDTO { Id = jt.Id, Name = jt.Name })
                .ToListAsync();
            return new ApiResponse<List<LookupDTO>>(200, items);
        }

        public async Task<ApiResponse<List<LookupDTO>>> GetLocationTypesAsync()
        {
            var items = await _context.TbJobLocationTypes
                .Select(lt => new LookupDTO { Id = lt.Id, Name = lt.Name })
                .ToListAsync();
            return new ApiResponse<List<LookupDTO>>(200, items);
        }

        public async Task<ApiResponse<List<CurrencyLookupDTO>>> GetCurrenciesAsync(string? search)
        {
            var query = _context.TbCurrencies.Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search));
            }

            var items = await query
                .OrderBy(c => c.Name)
                .Select(c => new CurrencyLookupDTO { Id = c.Id, Code = c.Code, Name = c.Name })
                .ToListAsync();
            return new ApiResponse<List<CurrencyLookupDTO>>(200, items);
        }

        public async Task<ApiResponse<List<CountryLookupDTO>>> GetCountriesAsync(string? search)
        {
            var query = _context.TbCountries.Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search));
            }

            var items = await query
                .OrderBy(c => c.Name)
                .Select(c => new CountryLookupDTO { Id = c.Id, Name = c.Name, Code = c.Code })
                .ToListAsync();
            return new ApiResponse<List<CountryLookupDTO>>(200, items);
        }

        public async Task<ApiResponse<List<LookupDTO>>> GetGovernatesAsync(int countryId, string? search)
        {
            var query = _context.TbGovernates.Where(g => g.CountryId == countryId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(g => g.Name.Contains(search));
            }

            var items = await query
                .OrderBy(g => g.Name)
                .Select(g => new LookupDTO { Id = g.Id, Name = g.Name })
                .ToListAsync();
            return new ApiResponse<List<LookupDTO>>(200, items);
        }

        public async Task<ApiResponse<List<SkillDTO>>> GetSkillsAsync(string? search)
        {
            var query = _context.TbSkills.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => s.Name.Contains(search));
            }

            var items = await query
                .OrderBy(s => s.Name)
                .Take(50)
                .Select(s => new SkillDTO { Id = s.Id, Name = s.Name })
                .ToListAsync();

            return new ApiResponse<List<SkillDTO>>(200, items);
        }

        // ==================== Helpers ====================

        private async Task HandleJobSkillsAsync(int jobId, List<int> skillIds, List<string> newSkillNames)
        {
            // Add existing skills
            foreach (var skillId in skillIds)
            {
                var skillExists = await _context.TbSkills.AnyAsync(s => s.Id == skillId);
                if (skillExists)
                {
                    _context.TbJobSkills.Add(new JobSkill { JobId = jobId, SkillId = skillId });
                }
            }

            // Create and add new skills
            foreach (var skillName in newSkillNames)
            {
                if (string.IsNullOrWhiteSpace(skillName)) continue;

                // Check if skill already exists by name
                var existing = await _context.TbSkills.FirstOrDefaultAsync(s => s.Name == skillName);
                if (existing != null)
                {
                    _context.TbJobSkills.Add(new JobSkill { JobId = jobId, SkillId = existing.Id });
                }
                else
                {
                    var newSkill = new Skill { Name = skillName };
                    _context.TbSkills.Add(newSkill);
                    await _context.SaveChangesAsync();
                    _context.TbJobSkills.Add(new JobSkill { JobId = jobId, SkillId = newSkill.Id });
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task CheckAndExpireJobsAsync(int employerId)
        {
            var expiredJobs = await _context.TbJobs
                .Where(j => j.EmployerId == employerId &&
                            j.JobStatusId == (int)JobStatusEnum.Published &&
                            j.ExpirationDate < DateTime.UtcNow)
                .ToListAsync();

            if (expiredJobs.Any())
            {
                foreach (var job in expiredJobs)
                {
                    job.JobStatusId = (int)JobStatusEnum.Expired;
                }
                await _context.SaveChangesAsync();
            }
        }
    }
}
