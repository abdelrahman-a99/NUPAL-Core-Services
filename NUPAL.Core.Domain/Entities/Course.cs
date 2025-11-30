namespace Nupal.Domain.Entities
{
    public class Course
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public double Credit { get; set; }
        public string Grade { get; set; }
        public double? Gpa { get; set; }
    }
}
