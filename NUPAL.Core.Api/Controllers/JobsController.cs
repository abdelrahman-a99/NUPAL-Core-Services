using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NUPAL.Core.Application.Interfaces;

namespace NUPAL.Core.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobsController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpGet]
        public async Task<IActionResult> GetJobs([FromQuery] string? what = null, [FromQuery] string? where = null)
        {
            var jobs = await _jobService.GetJobsAsync(what, where);
            return Ok(jobs);
        }
    }
}
