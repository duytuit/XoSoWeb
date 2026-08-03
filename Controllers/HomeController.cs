using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using XoSoWeb.Shares.BaseRepository;
using XoSoWeb.Shares;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

namespace XoSoWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly XoSoDBContext _context;
        private readonly RedisService _redisService;

        public HomeController(ILogger<HomeController> logger, RedisService redisService, XoSoDBContext context)
        {
            _logger = logger;
            _redisService = redisService;
        }

        /// <summary>
        /// List users
        /// </summary>
        /// <returns></returns>
        //[HttpGet]
        //public string Get([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        //{
        //return "Chào mừng bạn đến với chúng tôi....";
        //var value = _redisService.GetAsync("jtec_hn_database_update_AsyncKTNQ");
        //return value.Result ?? "No value found in Redis";
        //}
        [HttpGet("/")]
        public IActionResult Index()
        {
            ViewData["Message"] = "Chào mừng đến Web API + View1";
            return View();
        }
        [HttpGet("/privacy")]
        public IActionResult Privacy()
        {
            return View("Privacy");
        }
        [HttpGet("/support")]
        public IActionResult Support()
        {
            return View("Support");
        }
        [HttpPost("support")]
        public async Task<IActionResult> Contact([FromBody] ContactSupportRequest request)
        {
            return Ok(new
            {
                success = true,
                message = "Support request received."
            });
        }

    }
}
public class ContactSupportRequest
{
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Subject { get; set; }
    public string Content { get; set; }
}
