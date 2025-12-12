namespace NUPAL.Core.Application.DTOs
{
    public class JobDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string CompanyName { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string RedirectUrl { get; set; }
        public string Created { get; set; }
        public string Category { get; set; }
        public string ContractTime { get; set; } // e.g., full_time
        public string WorkType { get; set; } // e.g., remote, hybrid, on-site
    }
}
