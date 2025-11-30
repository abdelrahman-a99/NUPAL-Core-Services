namespace Nupal.Domain.Entities
{
    public class Semester
    {
        public string Term { get; set; }
        public bool Optional { get; set; }
        public List<Course> Courses { get; set; }
        public double SemesterCredits { get; set; }
        public double SemesterGpa { get; set; }
        public double CumulativeGpa { get; set; }
    }
}
