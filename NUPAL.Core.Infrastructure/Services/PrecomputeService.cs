using NUPAL.Core.Application.DTOs;
using NUPAL.Core.Application.Interfaces;
using Nupal.Domain.Entities;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Nupal.Core.Infrastructure.Services
{
    public class PrecomputeService : IPrecomputeService
    {
        private static readonly string[] SupportedTracks = new[] { "general", "big_data", "media" };
        private const string DefaultObjectiveProfile = "balanced";
        private const string RecommendationVariantSchemaVersion = "track-aware-v1";

        private readonly IStudentRepository _studentRepo;
        private readonly IRlJobRepository _jobRepo;
        private readonly IRlRecommendationRepository _recRepo;
        private readonly IRlService _rlService;

        public PrecomputeService(
            IStudentRepository studentRepo,
            IRlJobRepository jobRepo,
            IRlRecommendationRepository recRepo,
            IRlService rlService)
        {
            _studentRepo = studentRepo;
            _jobRepo = jobRepo;
            _recRepo = recRepo;
            _rlService = rlService;
        }

        public async Task<string> TriggerPrecomputeAsync(string studentId, bool isSimulation = false, int? episodes = null, string? targetTrack = null)
        {
            var student = await _studentRepo.GetByIdAsync(studentId)
                          ?? await _studentRepo.FindByEmailAsync(studentId); // Support ID or Email

            if (student == null)
                throw new KeyNotFoundException($"Student {studentId} not found");

            // Compute Hash of Education to prevent redundant training if needed
            var eduJson = JsonSerializer.Serialize(student.Education);
            // Fix: Store the "Clean" hash in the DB so SyncAll can compare apples-to-apples.
            // If we want to track sim/episodes, we should store them as separate columns in RlJob, not bake into the hash.
            var eduHash = ComputeSha256($"{RecommendationVariantSchemaVersion}|{eduJson}");

            // Create Job
            var job = new RlJob
            {
                StudentId = student.Account.Id,
                Status = JobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                EducationHash = eduHash,
                IsSimulation = isSimulation
            };

            await _jobRepo.CreateAsync(job);

            // Trigger Background Task
            _ = Task.Run(async () => await ProcessJobAsync(job.Id.ToString(), student, isSimulation, episodes, targetTrack));

            return job.Id.ToString();
        }

        public async Task<object> GetJobStatusAsync()
        {
            var jobs = await _jobRepo.GetActiveJobsAsync();
            return jobs.Select(j => new
            {
                JobId = j.Id.ToString(),
                j.StudentId,
                Status = j.Status.ToString(),
                CreatedAt = j.CreatedAt,
                StartedAt = j.StartedAt,
                FinishedAt = j.FinishedAt,
                ResultRecommendationId = j.ResultRecommendationId,
                j.Error
            });
        }

        public async Task<RlRecommendation?> GetRecommendationAsync(string id)
        {
            // Assuming we can add GetByIdAsync to IRlRecommendationRepository or use the collection directly if needed
            // For now, I'll rely on the repository interface update or a direct find if I can view the repo.
            // Let's first check the repo interface in the next step.
            return await _recRepo.GetByIdAsync(id);
        }

        public async Task<SyncResult> SyncAllStudentsAsync(bool isSimulation = false)
        {
            var students = (await _studentRepo.GetAllAsync())
                .Where(s => string.IsNullOrWhiteSpace(s.Account.Role) || s.Account.Role.ToLower() != "admin")
                .ToList();
            var result = new SyncResult { TotalStudents = students.Count() };

            foreach (var student in students)
            {
                // Logic:
                // 1. Calculate current hash.
                // 2. Check if latest job matches this hash and is Finished (Ready).
                // 3. If not, trigger.

                var eduJson = JsonSerializer.Serialize(student.Education);
                // Hash is always "production" (raw) hash to allow comparison
                var currentHash = ComputeSha256($"{RecommendationVariantSchemaVersion}|{eduJson}");

                var latestJob = await _jobRepo.GetLatestByStudentIdAsync(student.Account.Id);

                bool needsJob = false;

                if (latestJob == null)
                {
                    needsJob = true;
                }
                else
                {
                    // 1. Check if hash or mode changed
                    if (latestJob.EducationHash != currentHash ||
                        latestJob.Status == JobStatus.Failed ||
                        latestJob.IsSimulation != isSimulation)
                    {
                        needsJob = true;
                    }
                    else if (latestJob.Status == JobStatus.Ready && !string.IsNullOrEmpty(latestJob.ResultRecommendationId))
                    {
                        // 2. Even if job says "Ready", check if the recommendation document still exists in the DB
                        var recommendation = await _recRepo.GetByIdAsync(latestJob.ResultRecommendationId);
                        if (recommendation == null)
                        {
                            Console.WriteLine($"[DEBUG] SyncAll: Job {latestJob.Id} is Ready but Recommendation {latestJob.ResultRecommendationId} is missing. Re-triggering...");
                            needsJob = true;
                        }
                    }
                }

                if (needsJob)
                {
                     // Trigger job with requested mode (simulation or production)
                     // Await the trigger to prevent slamming the RL service and database with concurrent requests
                    await TriggerPrecomputeAsync(student.Account.Id, isSimulation, episodes: null);

                     // Optional: Add a small delay if the RL service is fragile
                    await Task.Delay(500);

                    result.TriggeredJobs++;
                    result.TriggeredStudentIds.Add(student.Account.Id);
                }
            }

            return result;
        }

        private async Task ProcessJobAsync(string jobId, Student student, bool isSimulation, int? episodes, string? targetTrack)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Job {jobId}: Starting track-aware processing...");
                await _jobRepo.UpdateStatusAsync(jobId, JobStatus.Running);

                var tracksToCompute = ResolveTracks(targetTrack);
                string? defaultRecommendationId = null;

                foreach (var track in tracksToCompute)
                {
                    Console.WriteLine($"[DEBUG] Job {jobId}: Computing {DefaultObjectiveProfile}+{track}...");
                    var request = MapToRlRequest(student, isSimulation, episodes, track, DefaultObjectiveProfile);

                    Console.WriteLine($"[DEBUG] Job {jobId}: Sending RL Request: {JsonSerializer.Serialize(request)}");
                    var response = await _rlService.GetRecommendationAsync(request);
                    Console.WriteLine($"[DEBUG] Job {jobId}: Received RL Response for track={track}");

                    var recommendation = MapToEntity(response, student.Account.Id, track, DefaultObjectiveProfile);
                    await _recRepo.CreateAsync(recommendation);
                    Console.WriteLine($"[DEBUG] Job {jobId}: Saved {DefaultObjectiveProfile}+{track} Recommendation ID: {recommendation.Id}");

                    // Keep the legacy pointer useful. Prefer general if all variants were computed.
                    if (defaultRecommendationId == null || track == "general")
                    {
                        defaultRecommendationId = recommendation.Id.ToString();
                    }
                }

                if (string.IsNullOrWhiteSpace(defaultRecommendationId))
                {
                    throw new InvalidOperationException("No recommendation variants were created.");
                }

                await _jobRepo.UpdateResultAsync(jobId, defaultRecommendationId);

                student.LatestRecommendationId = defaultRecommendationId;
                await _studentRepo.UpsertAsync(student);

                Console.WriteLine($"[DEBUG] Job {jobId}: Finished successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Job {jobId}: FAILED with error: {ex}");
                try
                {
                    await _jobRepo.UpdateStatusAsync(jobId, JobStatus.Failed, ex.Message);
                }
                catch (Exception finalEx)
                {
                    Console.WriteLine($"[CRITICAL] Job {jobId}: Failed to update status to Failed. Error: {finalEx}");
                }
            }
        }

        private static List<string> ResolveTracks(string? requestedTrack)
        {
            if (!string.IsNullOrWhiteSpace(requestedTrack))
            {
                return new List<string> { NormalizeTargetTrack(requestedTrack) };
            }

            // First production strategy: compute balanced once per track, not all profile×track combinations.
            return SupportedTracks.ToList();
        }

        private static string NormalizeTargetTrack(string? targetTrack)
        {
            var raw = (targetTrack ?? "general").Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            return raw switch
            {
                "bigdata" or "big_data" or "big_data_track" => "big_data",
                "media" or "media_informatics" or "media_track" => "media",
                "general" or "general_track" => "general",
                _ => "general"
            };
        }

        private RlTrainingRequest MapToRlRequest(Student student, bool isSimulation, int? episodes, string targetTrack, string objectiveProfile)
        {
            var edu = student.Education;

            // Simulation Logic: Truncate to N-2 semesters if simulation is requested
            var semesters = edu.Semesters ?? new List<Semester>();
            var totalCredits = edu.TotalCredits;
            var numSemesters = edu.NumSemesters;

            if (isSimulation && semesters.Count > 2)
            {
                // Simulate being 2 semesters back
                int take = semesters.Count - 2;
                semesters = semesters.Take(take).ToList();
                // Recalculate credits (approximate)
                totalCredits = semesters.Sum(s => s.SemesterCredits);
                numSemesters = semesters.Count;
            }

            var rlEdu = new RlEducation
            {
                TotalCredits = totalCredits,
                NumSemesters = numSemesters,
                Semesters = new Dictionary<string, RlSemester>()
            };

            foreach (var sem in semesters)
            {
               rlEdu.Semesters[sem.Term] = new RlSemester
               {
                   CumulativeGpa = sem.CumulativeGpa,
                   SemesterGpa = sem.SemesterGpa,
                   SemesterCredits = sem.SemesterCredits,
                   Optional = sem.Optional,
                   Courses = sem.Courses.Select(c => new RlCourse
                   {
                       CourseId = c.CourseId,
                       CourseName = c.CourseName,
                       Credit = c.Credit,
                       Grade = c.Grade,
                       Gpa = c.Gpa ?? 0
                   }).ToList()
               };
            }

            // Preserve current caller behavior: explicit episodes wins; existing default remains light.
            int epCount = episodes ?? 1;

            return new RlTrainingRequest
            {
                StudentId = student.Account.Id,
                Education = rlEdu,
                Episodes = epCount,
                PretrainSteps = epCount,
                MaxSemesters = 8,
                Seed = 42,
                Profile = objectiveProfile,
                Profiles = new List<string> { objectiveProfile },
                TargetTrack = targetTrack
            };
        }

        private RlRecommendation MapToEntity(RlTrainingResponse response, string studentId, string targetTrack, string objectiveProfile)
        {
            var finalCumGpa = response.Metadata?.FinalCumGpa
                              ?? response.Metadata?.BestEpisode?.CumGpa
                              ?? 0;
            var finalTotalCredits = response.Metadata?.FinalTotalCredits
                                    ?? response.Metadata?.BestEpisode?.TotalCredits
                                    ?? response.Metadata?.TotalCredits
                                    ?? 0;
            var graduated = (response.Metadata?.Status == "already_finished")
                            || (response.Metadata?.Graduated ?? false)
                            || (response.Metadata?.BestEpisode?.Graduated ?? false);

            return new RlRecommendation
            {
                StudentId = studentId,
                CreatedAt = DateTime.UtcNow,
                TargetTrack = NormalizeTargetTrack(response.Metadata?.TargetTrack ?? targetTrack),
                ObjectiveProfile = (response.Metadata?.Profile ?? response.DefaultProfile ?? objectiveProfile ?? DefaultObjectiveProfile).Trim().ToLowerInvariant(),
                Courses = (response.RecommendedSlates != null && response.RecommendedSlates.Any())
                          ? response.RecommendedSlates.First()
                          : new List<string>(),
                TermIndex = response.Terms?.FirstOrDefault()?.Term ?? 0,
                SlatesByTerm = response.Terms?.Select(t => new TermRecommendation
                {
                    Term = t.Term,
                    Slate = t.Slate
                }).ToList(),
                Metrics = new RecommendationMetrics
                {
                    CumGpa = finalCumGpa,
                    TotalCredits = finalTotalCredits,
                    Graduated = graduated,
                    GradFlags = ConvertGradFlags(response.Metadata?.GradFlags)
                }
            };
        }

        private static Dictionary<string, object> ConvertGradFlags(JsonElement? gradFlags)
        {
            if (!gradFlags.HasValue || gradFlags.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return new Dictionary<string, object>();
            }

            var element = gradFlags.Value;
            if (element.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, object>();
                foreach (var prop in element.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number when prop.Value.TryGetDouble(out var d) => d,
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Array => prop.Value.ToString(),
                        JsonValueKind.Object => prop.Value.ToString(),
                        _ => prop.Value.ToString()
                    };
                }
                return result;
            }

            return new Dictionary<string, object>();
        }

        private static string ComputeSha256(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
