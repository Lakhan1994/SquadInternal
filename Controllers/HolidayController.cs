using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Filters;
using SquadInternal.Models;

namespace SquadInternal.Controllers
{
    [SessionAuthorize(SessionAuthMode.EmployeeOnly)]
    public class HolidayController : Controller
    {
        private readonly AppDbContext _db;

        public HolidayController(AppDbContext db)
        {
            _db = db;
        }

        // ================= INDEX =================
        public async Task<IActionResult> Index(int? year)
        {
            int selectedYear = year ?? DateTime.Now.Year;

            var holidays = await _db.SquadHolidays
                .Where(h => h.IsActive && h.HolidayDate.Year == selectedYear)
                .OrderBy(h => h.HolidayDate)
                .ToListAsync();

            ViewBag.SelectedYear = selectedYear;
            ViewBag.Total = holidays.Count;
            ViewBag.National = holidays.Count(h => h.Type == "National");
            ViewBag.Optional = holidays.Count(h => h.Type == "Optional");
            ViewBag.Upcoming = holidays.Count(h => h.HolidayDate >= DateTime.Today);

            return View(holidays);
        }


        // ================= CREATE =================
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SquadHoliday model)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.IsActive = true; 

            _db.SquadHolidays.Add(model);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ================= EDIT =================
        public async Task<IActionResult> Edit(int id)
        {
            var holiday = await _db.SquadHolidays
                .FirstOrDefaultAsync(h => h.Id == id && h.IsActive);

            if (holiday == null)
                return NotFound();

            return View(holiday);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SquadHoliday model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var holiday = await _db.SquadHolidays.FindAsync(model.Id);
            if (holiday == null)
                return NotFound();

            holiday.Name = model.Name;
            holiday.HolidayDate = model.HolidayDate;
            holiday.Type = model.Type;

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ================= DELETE (Soft) =================
        public async Task<IActionResult> Delete(int id)
        {
            var holiday = await _db.SquadHolidays.FindAsync(id);
            if (holiday == null)
                return NotFound();

            holiday.IsActive = false;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
       

namespace SquadInternal.Controllers
    {
        [AuthorizeEmployee]
        public class HolidayController : Controller
        {
            private readonly AppDbContext _db;

            public HolidayController(AppDbContext db)
            {
                _db = db;
            }

            public IActionResult Index()
            {
                var holidays = _db.SquadHolidays
                    .Where(h => h.IsActive)
                    .OrderBy(h => h.HolidayDate)
                    .ToList();

                return View(holidays);
            }
        }
    }

}

