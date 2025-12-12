using NUPAL.Core.Application.DTOs;
using NUPAL.Core.Application.Interfaces;
using Nupal.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NUPAL.Core.Application.Services
{
    public class StudentService : IStudentService
    {
        private readonly IStudentRepository _repo;

        public StudentService(IStudentRepository repo)
        {
            _repo = repo;
        }

        public async Task UpsertStudentAsync(ImportStudentDto dto)
        {
            var semesters = dto.Education.Semesters?.Select(kv => new Semester
            {
                Term = kv.Key,
                Optional = kv.Value.Optional,
                Courses = kv.Value.Courses?.Select(c => new Course
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    Credit = c.Credit,
                    Grade = c.Grade,
                    Gpa = c.Gpa
                }).ToList() ?? new List<Course>(),
                SemesterCredits = kv.Value.SemesterCredits,
                SemesterGpa = kv.Value.SemesterGpa,
                CumulativeGpa = kv.Value.CumulativeGpa
            }).ToList() ?? new List<Semester>();

            var student = new Student
            {
                Account = new Account
                {
                    Id = dto.Account.Id,
                    Email = dto.Account.Email.ToLower(),
                    Name = dto.Account.Name,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Account.Password, workFactor: 10)
                },
                Education = new Education
                {
                    TotalCredits = dto.Education.TotalCredits,
                    NumSemesters = dto.Education.NumSemesters,
                    Semesters = semesters
                }
            };

            await _repo.UpsertAsync(student);
        }

        public async Task<StudentDto> GetStudentByEmailAsync(string email)
        {
            var s = await _repo.FindByEmailAsync(email.ToLower());
            if (s == null) return null;
            return MapToDto(s);
        }

        public async Task<AuthResponseDto> AuthenticateAsync(LoginDto loginDto, string jwtKey, string jwtIssuer, string jwtAudience)
        {
            var s = await _repo.FindByEmailAsync(loginDto.Email.ToLower());
            if (s == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, s.Account.PasswordHash))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, s.Account.Id),
                    new Claim(ClaimTypes.Email, s.Account.Email),
                    new Claim(ClaimTypes.Name, s.Account.Name)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = jwtIssuer,
                Audience = jwtAudience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return new AuthResponseDto
            {
                Token = tokenString,
                Student = MapToDto(s)
            };
        }

        private StudentDto MapToDto(Student s)
        {
            return new StudentDto
            {
                Id = s.Account.Id,
                Account = new AccountDto
                {
                    Id = s.Account.Id,
                    Email = s.Account.Email,
                    Name = s.Account.Name
                },
                Education = new EducationDto
                {
                    TotalCredits = s.Education.TotalCredits,
                    NumSemesters = s.Education.NumSemesters,
                    Semesters = s.Education.Semesters.Select(sem => new SemesterDto
                    {
                        Term = sem.Term,
                        Optional = sem.Optional,
                        Courses = sem.Courses.Select(c => new CourseDto
                        {
                            CourseId = c.CourseId,
                            CourseName = c.CourseName,
                            Credit = c.Credit,
                            Grade = c.Grade,
                            Gpa = c.Gpa
                        }).ToList(),
                        SemesterCredits = sem.SemesterCredits,
                        SemesterGpa = sem.SemesterGpa,
                        CumulativeGpa = sem.CumulativeGpa
                    }).ToList()
                }
            };
        }
    }
}
