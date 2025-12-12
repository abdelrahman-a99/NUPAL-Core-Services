namespace NUPAL.Core.Application.DTOs
{
    public class StudentDto
    {
        public string Id { get; set; }
        public AccountDto Account { get; set; }
        public EducationDto Education { get; set; }
    }

    public class AccountDto
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
    }

    public class EducationDto
    {
        public double TotalCredits { get; set; }
        public int NumSemesters { get; set; }
        public List<SemesterDto> Semesters { get; set; }
    }

    public class SemesterDto
    {
        public string Term { get; set; }
        public bool Optional { get; set; }
        public List<CourseDto> Courses { get; set; }
        public double SemesterCredits { get; set; }
        public double SemesterGpa { get; set; }
        public double CumulativeGpa { get; set; }
    }

    public class CourseDto
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public double Credit { get; set; }
        public string Grade { get; set; }
        public double? Gpa { get; set; }
    }
}
