using Microsoft.AspNetCore.Mvc;

namespace MCP.ADB2C.Client.Controllers
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
