using Microsoft.AspNetCore.Mvc;
using NUPAL.Core.Application.Interfaces;    
using Nupal.Domain.Entities;

namespace NUPAL.Core.API.Controllers    
{
    [ApiController]
    [Route("api/students")]
    public class StudentsController : ControllerBase
    {
        private readonly IStudentService _service;

        public StudentsController(IStudentService service)
        {
            _service = service;
        }

        public class ImportRequest
        {
            public AccountJson account { get; set; }
            public EducationJson education { get; set; }
        }

        public class AccountJson
        {
            public string id { get; set; }
            public string email { get; set; }
            public string name { get; set; }
            public string password { get; set; }
        }

        public class EducationJson
        {
            public double total_credits { get; set; }
            public int num_semesters { get; set; }
            public Dictionary<string, SemesterJson> semesters { get; set; }
        }

        public class SemesterJson
        {
            public bool optional { get; set; }
            public List<CourseJson> courses { get; set; }
            public double semester_credits { get; set; }
            public double semester_gpa { get; set; }
            public double cumulative_gpa { get; set; }
        }

        public class CourseJson
        {
            public string course_id { get; set; }
            public string course_name { get; set; }
            public double credit { get; set; }
            public string grade { get; set; }
            public double? gpa { get; set; }
        }

        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] ImportRequest req)
        {
            try
            {
                if (req == null || req.account == null || req.education == null || string.IsNullOrWhiteSpace(req.account.email) || string.IsNullOrWhiteSpace(req.account.password) || string.IsNullOrWhiteSpace(req.account.id))
                    return BadRequest(new { error = "missing_fields" });

                var semesters = req.education.semesters?.Select(kv => new Semester
                {
                    Term = kv.Key,
                    Optional = kv.Value.optional,
                    Courses = kv.Value.courses?.Select(c => new Course
                    {
                        CourseId = c.course_id,
                        CourseName = c.course_name,
                        Credit = c.credit,
                        Grade = c.grade,
                        Gpa = c.gpa
                    }).ToList() ?? new List<Course>(),
                    SemesterCredits = kv.Value.semester_credits,
                    SemesterGpa = kv.Value.semester_gpa,
                    CumulativeGpa = kv.Value.cumulative_gpa
                }).ToList() ?? new List<Semester>();

                var student = new Student
                {
                    Account = new Account
                    {
                        Id = req.account.id,
                        Email = req.account.email.ToLower(),
                        Name = req.account.name,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.account.password, workFactor: 10)
                    },
                    Education = new Education
                    {
                        TotalCredits = req.education.total_credits,
                        NumSemesters = req.education.num_semesters,
                        Semesters = semesters
                    }
                };

                await _service.UpsertStudentAsync(student);
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "server_error", message = ex.Message });
            }
        }

        public class LoginBody
        {
            public string email { get; set; }
            public string password { get; set; }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginBody body)
        {
            try
            {
                if (body == null || string.IsNullOrWhiteSpace(body.email) || string.IsNullOrWhiteSpace(body.password))
                    return BadRequest(new { error = "missing_fields" });

                var s = await _service.FindByEmailAsync(body.email.ToLower());
                if (s == null) return Unauthorized(new { error = "invalid_credentials" });

                var ok = await _service.VerifyPasswordAsync(s, body.password);
                if (!ok) return Unauthorized(new { error = "invalid_credentials" });

                if (s.Account != null) s.Account.PasswordHash = null;
                return Ok(new { ok = true, student = s });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "server_error", message = ex.Message });
            }
        }

        [HttpGet("by-email/{email}")]
        public async Task<IActionResult> GetByEmail([FromRoute] string email)
        {
            try
            {
                var s = await _service.FindByEmailAsync(email.ToLower());
                if (s == null) return NotFound(new { error = "not_found" });
                if (s.Account != null) s.Account.PasswordHash = null;
                return Ok(s);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "server_error", message = ex.Message });
            }
        }
    }
}
