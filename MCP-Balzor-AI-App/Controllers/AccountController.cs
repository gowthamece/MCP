using Microsoft.AspNetCore.Mvc;

namespace MCP_Balzor_AI_App.Controllers
{
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        public IActionResult SignedOut()
        {
            return Redirect("/");
        }
    }
}
