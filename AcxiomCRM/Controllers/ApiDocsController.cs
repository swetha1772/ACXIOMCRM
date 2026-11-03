using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ApiDocsController : Controller
    {
        [HttpGet("/api-docs")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
