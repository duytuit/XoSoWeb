using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace XoSoWeb.Controllers
{
    public class ErrorController : Controller
    {
        [HttpGet("/404")]
        [HttpHead("/404")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult PageNotFound()
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            Response.Headers["X-Robots-Tag"] = "noindex, follow";
            return View();
        }
    }
}
