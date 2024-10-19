using Domain.Entities;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeveloperController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public DeveloperController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet("{id}")]
        public IActionResult GetPopularDevelopers([FromQuery] int count)
        {
            var popularDevelopers = _unitOfWork.Developers.GetPopularDevelopers(count);
            return Ok(popularDevelopers);
        }
        [HttpGet]
        public IActionResult GetAllDataDeveloper()
        {
            var listData = _unitOfWork.Developers.GetAllAsync();
            return Ok(listData);
        }

        [HttpPost]
        public IActionResult AddDeveloperAndProject()
        {
            var developer = new Developer
            {
                Followers = 35,
                Name = "Developer"
            };
            var project = new Project
            {
                Name = "Repository Pattern"
            };
            _unitOfWork.Developers.AddAsync(developer);
            _unitOfWork.Projects.AddAsync(project);
            _unitOfWork.Complete();
            return Ok();
        }
    }
}
