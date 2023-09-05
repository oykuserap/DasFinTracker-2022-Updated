using DasFinTracker.Models;
using DasFinTracker.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace DasFinTracker.Controllers
{
    public class DashboardController : Controller
    {

        private readonly ApplicationDbContext _context;
        private LanguageService _localization;
       

        public DashboardController(ApplicationDbContext context, LanguageService localization)
        {
            _context = context;
            _localization = localization;
        }

        public IActionResult ChangeLanguage(string culture)
        {
            Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)), new CookieOptions()
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                });
            return Redirect(Request.Headers["Referer"].ToString());
        }

        public async Task<ActionResult> Index()
        {
            
            //Last 7 Days
            DateTime StartDate = DateTime.Today.AddDays(-6);
            DateTime EndDate = DateTime.Today;

            List<Transaction> SelectedTransactions = await _context.Transactions
                .Include(x => x.Category)
                .Where(y => y.Date >= StartDate && y.Date <= EndDate)
                .ToListAsync();

            //culture:

            ViewBag.Welcome = _localization.Getkey("TotalIncome").Value;
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;

            //CultureInfo culture = CultureInfo.CreateSpecificCulture("tr-TR");
            //CultureInfo culture = CultureInfo.CurrentCulture; 
            //culture.NumberFormat.CurrencyNegativePattern = 1;

            //Total Income
            int TotalIncome = SelectedTransactions
                .Where(i => i.Category?.Type == "Income")
                .Sum(j => j.Amount);
            //ViewBag.TotalIncome = TotalIncome.ToString("C0");
            ViewBag.TotalIncome = String.Format(culture, "{0:C0}", TotalIncome);


            //Total Expense
            int TotalExpense = SelectedTransactions
                .Where(i => i.Category?.Type == "Expense")
                .Sum(j => j.Amount);
            //ViewBag.TotalExpense = TotalExpense.ToString("C0");
            ViewBag.TotalExpense = String.Format(culture, "{0:C0}", TotalExpense);


            //Balance
            int Balance = TotalIncome - TotalExpense;
            ViewBag.Balance = String.Format(culture, "{0:C0}", Balance);

            //Doughnut Chart - Expense By Category
            ViewBag.DoughnutChartData = SelectedTransactions
                .Where(i => i.Category?.Type == "Expense")
                .GroupBy(j => j.Category?.CategoryId)
                .Select(k => new
                {
                    categoryTitleWithIcon = k.First().Category.Icon + " " + k.First().Category.Title,
                    amount = k.Sum(j => j.Amount),
                    //formattedAmount = k.Sum(j => j.Amount).ToString("C0"),
                    formattedAmount = String.Format(culture, "{0:C0}", k.Sum(j => j.Amount)),
        })
                .OrderByDescending(l => l.amount)
                .ToList();

            //Spline Chart - Income vs Expense

            //Income
            List<SplineChartData> IncomeSummary = SelectedTransactions
                .Where(i => i.Category.Type == "Income")
                .GroupBy(j => j.Date)
                .Select(k => new SplineChartData()
                {
                    day = k.First().Date.ToString("dd-MMM"),
                    income = k.Sum(l => l.Amount)
                })
                .ToList();

            //Expense
            List<SplineChartData> ExpenseSummary = SelectedTransactions
                .Where(i => i.Category.Type == "Expense")
                .GroupBy(j => j.Date)
                .Select(k => new SplineChartData()
                {
                    day = k.First().Date.ToString("dd-MMM"),
                    expense = k.Sum(l => l.Amount)
                })
                .ToList();

            //Combine Income & Expense
            string[] Last7Days = Enumerable.Range(0, 7)
                .Select(i => StartDate.AddDays(i).ToString("dd-MMM"))
                .ToArray();

            ViewBag.SplineChartData = from day in Last7Days
                                      join income in IncomeSummary on day equals income.day into dayIncomeJoined
                                      from income in dayIncomeJoined.DefaultIfEmpty()
                                      join expense in ExpenseSummary on day equals expense.day into expenseJoined
                                      from expense in expenseJoined.DefaultIfEmpty()
                                      select new
                                      {
                                          day = day,
                                          income = income == null ? 0 : income.income,
                                          expense = expense == null ? 0 : expense.expense,
                                      };
            //Recent Transactions
            ViewBag.RecentTransactions = await _context.Transactions
                .Include(i => i.Category)
                .OrderByDescending(j => j.Date)
                .Take(5)
                .ToListAsync();

            ViewBag.CultureData = new CultureDetails().Cultures();

            return View();
        }
    }

    public class SplineChartData
    {
        public string day;
        public int income;
        public int expense;

    }
}
