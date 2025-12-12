using System.Collections.Generic;
using System.Threading.Tasks;
using NUPAL.Core.Application.DTOs;

namespace NUPAL.Core.Application.Interfaces
{
    public interface IJobService
    {
        Task<IEnumerable<JobDto>> GetJobsAsync(string? what = null, string? where = null, string? country = null);
    }
}
