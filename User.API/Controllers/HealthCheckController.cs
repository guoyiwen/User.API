using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace User.API.Controllers
{
  [Route("[Controller]")]
  public class HealthCheckController : Controller
  {
    [HttpGet("")]
    [HttpHead("")]
    public IActionResult Ping()
    {
      return Ok();
    }

      [HttpGet("versions")] 
      public IActionResult GetVersions()
      {
          return Json("hello My V1.0");
      }
    }
}