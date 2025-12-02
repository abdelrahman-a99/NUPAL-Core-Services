using Microsoft.AspNetCore.Mvc;
using NUPAL.Core.Application.Interfaces;    
using Nupal.Domain.Entities;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using System.Net.Mail;

namespace NUPAL.Core.API.Controllers    
{
    [ApiController]
    [Route("api/students")]
    public class StudentsController : ControllerBase
    {
        private readonly IStudentService _service;
        private readonly IConfiguration _config;

        public StudentsController(IStudentService service, IConfiguration config)
        {
            _service = service;
            _config = config;
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
                if (body == null)
                    return BadRequest(new { error = "missing_fields" });

                var email = body.email?.Trim().ToLower();
                var password = body.password ?? string.Empty;

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    return BadRequest(new { error = "missing_fields" });

                // Basic email format validation
                bool validEmail;
                try { var addr = new MailAddress(email); validEmail = addr.Address == email; } catch { validEmail = false; }
                if (!validEmail)
                    return BadRequest(new { error = "invalid_email" });

                // Basic password policy (min length)
                if (password.Length < 6)
                    return BadRequest(new { error = "invalid_password_format" });

                var s = await _service.FindByEmailAsync(email);
                if (s == null) return Unauthorized(new { error = "incorrect_email_or_password" });

                var ok = await _service.VerifyPasswordAsync(s, password);
                if (!ok) return Unauthorized(new { error = "incorrect_email_or_password" });

                if (s.Account != null) s.Account.PasswordHash = null;

                var tokenHandler = new JwtSecurityTokenHandler();
                var keyValue = _config["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key configuration");
                var key = Encoding.UTF8.GetBytes(keyValue);
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, s.Account.Id),
                        new Claim(ClaimTypes.Email, s.Account.Email),
                        new Claim(ClaimTypes.Name, s.Account.Name)
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("Missing Jwt:Issuer configuration"),
                    Audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("Missing Jwt:Audience configuration"),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                return Ok(new { ok = true, token = tokenString, student = s });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "server_error", message = ex.Message });
            }
        }

        [Authorize]
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
