using System.Text.Json.Serialization;

namespace NUPAL.Core.Application.DTOs
{
    public class ImportStudentDto
    {
        [JsonPropertyName("account")]
        public AccountJson Account { get; set; }

        [JsonPropertyName("education")]
        public EducationJson Education { get; set; }
    }

    public class AccountJson
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }
    }

    public class EducationJson
    {
        [JsonPropertyName("total_credits")]
        public double TotalCredits { get; set; }

        [JsonPropertyName("num_semesters")]
        public int NumSemesters { get; set; }

        [JsonPropertyName("semesters")]
        public Dictionary<string, SemesterJson> Semesters { get; set; }
    }

    public class SemesterJson
    {
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }

        [JsonPropertyName("courses")]
        public List<CourseJson> Courses { get; set; }

        [JsonPropertyName("semester_credits")]
        public double SemesterCredits { get; set; }

        [JsonPropertyName("semester_gpa")]
        public double SemesterGpa { get; set; }

        [JsonPropertyName("cumulative_gpa")]
        public double CumulativeGpa { get; set; }
    }

    public class CourseJson
    {
        [JsonPropertyName("course_id")]
        public string CourseId { get; set; }

        [JsonPropertyName("course_name")]
        public string CourseName { get; set; }

        [JsonPropertyName("credit")]
        public double Credit { get; set; }

        [JsonPropertyName("grade")]
        public string Grade { get; set; }

        [JsonPropertyName("gpa")]
        public double? Gpa { get; set; }
    }
}
