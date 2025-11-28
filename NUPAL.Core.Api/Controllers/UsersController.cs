using Microsoft.AspNetCore.Mvc;
using Nupal.Application.Interfaces;

namespace Nupal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult GetUsers()
        {
            var users = _service.GetAllUsers();
            return Ok(users);
        }
    }
}
