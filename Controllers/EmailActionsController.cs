using Microsoft.AspNetCore.Mvc;

namespace SquadInternal.Controllers
{
    public class EmailActionsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
