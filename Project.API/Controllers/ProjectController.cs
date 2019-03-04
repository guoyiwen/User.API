using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Project.API.Applications.Commands;
using Project.API.Applications.Queries;
using Project.API.Applications.Services;
using Project.Domain.AggregatesModel;

namespace Project.API.Controllers
{
    [Route("api/projects")]
    public class ProjectController : BaseController
    {

        private readonly IMediator _mediator;
        private readonly ICommandService _commandService;
        private readonly IProjectQueries _projectQueries;


        public ProjectController(IMediator mediator, ICommandService commandService, IProjectQueries projectQueries)
        {
            _mediator = mediator;
            _commandService = commandService;
            _projectQueries = projectQueries;
        }



        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            var result = await _projectQueries.GetProjectListByUserIdAsync(UserIdentity.UserId);
            return Ok(result);
        }

        [HttpGet("my/{projectId}")]
        public async Task<IActionResult> GetMyProjectDetail(int projectId)
        {
            var result = await _projectQueries.GetProjectDetailAsync(projectId);
            if (result.UserId == UserIdentity.UserId)
            {
                return Ok(result);
            }


            return BadRequest("不具有查看当前项目的权限");
        }

        [HttpGet("recommends/{projectId}")] 
        public async Task<IActionResult> RecommandProjectDetail(int projectId)
        {
            var b = await _commandService.IsRecommandProject(projectId, UserIdentity.UserId);
            if (!b) return BadRequest("无权查看此项目");
            var project = await _projectQueries.GetProjectDetailAsync(projectId);
            if (project == null) return NotFound();
            return Ok(project);
        }

        [HttpPost("")]
        public async Task<IActionResult> CreatProject([FromBody] Domain.AggregatesModel.Project project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var cmd = new CreateProjectCommand() { Project = project };
            cmd.Project.UserId = UserIdentity.UserId;
            var result=  await _mediator.Send(cmd);
            return Ok(result);
        }

        [HttpPut("view/{projectId}")]
        public async Task<IActionResult>  ViewProject(int projectId)
        {
            if (!await _commandService.IsRecommandProject(projectId, UserIdentity.UserId))
            {
                return BadRequest("不具有查看当前项目的权限");
            }

            var cmd = new ViewProjectCommand()
            {
                ProjectViewer = new ProjectViewer()
                {
                    Avator = UserIdentity.Avatar,
                    ProjectId = projectId,
                    UserName = UserIdentity.Name,
                    UserId = UserIdentity.UserId,
                    CreateTime = DateTime.Now
                }
            };
            await _mediator.Send(cmd);
            return Ok();
        }



        [HttpPut("join/{projectId}")]
        public async Task<IActionResult> JoinProject([FromBody] ProjectContributor contributor)
        {
            if (contributor == null) throw new ArgumentNullException(nameof(contributor));
            if (!await _commandService.IsRecommandProject(contributor.ProjectId, UserIdentity.UserId))
            {
                return BadRequest("不具有查看当前项目的权限");
            }
            var cmd = new JoinProjectCommand() { ProjectContributor = contributor };
            await _mediator.Send(cmd);
            return Ok();

        }



    }
}
