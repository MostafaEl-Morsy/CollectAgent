// Controllers/HomeController.cs
using CollectAgent.Models;
using CollectAgent.ViewModels;
using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authorization; // <<< أضف هذا
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims; // <<< أضف هذا

namespace CollectAgent.Controllers
{
    [Authorize] // <<< هذا السطر يحمي الـ Controller بالكامل
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CollectAgentDBContext _context;

        public HomeController(ILogger<HomeController> logger, CollectAgentDBContext context)
        {
            _logger = logger;
            _context = context;
        }
        // --- دالة مساعدة لجلب بيانات المستخدم الحالي ---
        // هذا أفضل من تكرار الكود في كل Action
        private (int UserId, int UserTyp, int? CompanyId, int? BranchId) GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirstValue(ClaimTypes.Role); // تم تعيينه كـ Role في AccountController
            var companyIdClaim = User.FindFirstValue("CompanyId");
            var branchIdClaim = User.FindFirstValue("BranchId");

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userTypeClaim))
            {
                // هذا لا يجب أن يحدث لمستخدم مسجل دخوله، ولكنه احتياط جيد
                throw new InvalidOperationException("بيـانـات المـستخـدم غـير مـوجـودة");
            }

            // تحقق أساسي من وجود المستخدم
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return (0, 0, null, null);
            }

            int.TryParse(userIdClaim, out int userId);
            int.TryParse(userTypeClaim, out int userType);
            // استخدام null-able int مباشرة
            int? companyId = null;
            if (int.TryParse(companyIdClaim, out int cId))
            {
                companyId = cId;
            }
            int? branchId = null;
            if (int.TryParse(branchIdClaim, out int bId))
            {
                branchId = bId;
            }

            return (userId, userType, companyId, branchId);
        }

        [Authorize(Roles = "SuperAdmin,1,2,3,4,5,6,7,8,9,10,11")]
        public IActionResult Index()
        {
            // فحص هل المستخدم الحالي هو SuperAdmin؟
            if (User.IsInRole("SuperAdmin"))
            {
                // توجيه الأدمن لصفحة خاصة به أو عرض محتوى مختلف
                ViewData["IsAdmin"] = true;
                return View(); // أو return View("AdminDashboard");
            }
            //// ✅✅✅ سطر التشخيص الوحيد والصحيح هنا ✅✅✅
            //var sessionName = HttpContext.Session.GetString("FullName");
            //_logger.LogWarning("SESSION DATA READ in Home.Index. FullName is: '{SessionName}'", sessionName ?? "NULL!");
            // على سبيل المثال، يمكنك تمرير البيانات إلى الـ View
            ViewData["FullNameFromClaims"] = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.UserIdFromClaims = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["IsAdmin"] = false;

            return View();
        }

        public IActionResult TreansactionIndex()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }
            return View();
        }

        public async Task<IActionResult> DashBoard()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }
            // جلب بيانات خاصة بالـ Dashboard بناءً على نوع المستخدم
            var dashboardData = new DashboardViewModel();
            //if (currentUserType == 1) // مثال: نوع مستخدم معين
            //{
            //    dashboardData.SomeData = await _context.SomeEntities
            //        .Where(e => e.CompanyId == currentUserCompanyId)
            //        .ToListAsync();
            //}
            //else if (currentUserType == 2) // مثال: نوع مستخدم آخر
            //{
            //    dashboardData.SomeData = await _context.SomeEntities
            //        .Where(e => e.BranchId == currentUserBranchId)
            //        .ToListAsync();
            //}
            // يمكنك إضافة المزيد من الشروط بناءً على أنواع المستخدمين الأخرى
            return View(dashboardData);
        }

        // Controllers/HomeController.cs
        public async Task<IActionResult> InventoryDetails()
        {
            var userIdString = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdString);

            // 1. حساب الأرصدة (POS) - أصول
            var posBalance = await _context.TblRegistrations
                .Where(r => r.UserFk == userId && r.IsActive == true)
                .SumAsync(r => (double?)r.Balance) ?? 0;

            // 2. مديونية الوكيل للشركة (Indebtedness) - التزامات
            // التعديل: لن نضرب في -1، سنبقيها موجبة لنعرضها كـ "دين" صريح
            var userAcc = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == userId && a.IsActive == true);
            double companyDebt = (double)(userAcc?.Indebtedness ?? 0);

            // 3. حساب محافظ الكاش والبنك - أصول
            double cashWallets = await _context.TblServProvRegs
                .Where(p => p.UserFk == userId && p.ServProvFkNavigation.ProvTypeFk == 3 && p.IsActive == true)
                .SumAsync(p => (double?)p.Balance) ?? 0;

            double bankWallets = await _context.TblServProvRegs
                .Where(p => p.UserFk == userId && p.ServProvFkNavigation.ProvTypeFk == 4 && p.IsActive == true)
                .SumAsync(p => (double?)p.Balance) ?? 0;

            // 4. حساب أرصدة التجار التابعين
            var subUserIds = await _context.TblUsers
                .Where(u => u.ParentUser == userId)
                .Select(u => u.Id)
                .ToListAsync();

            var subUsersBalances = await _context.TblAccounts
                .Where(a => subUserIds.Contains(a.UserIdFk) && a.IsActive == true)
                .Select(a => (double)(a.Indebtedness ?? 0))
                .ToListAsync();

            // مبالغ على التجار (وكيل أعطاهم رصيد ولم يدفعوا) - أصول
            double merchantsDebts = subUsersBalances.Where(b => b > 0).Sum();

            // مبالغ للتجار (دفعوا مقدم أو لهم عمولات) - التزامات
            double merchantsCredits = Math.Abs(subUsersBalances.Where(b => b < 0).Sum());

            // 5. المعادلة المحاسبية الصحيحة للمركز المالي:
            // إجمالي الأصول = رصيد الأجهزة + محافظ الكاش + المحافظ البنكية + الديون التي على التجار
            double totalAssets = posBalance + cashWallets + bankWallets + merchantsDebts;

            // إجمالي الالتزامات = ديون الوكيل للشركة + الأرصدة المستحقة للتجار
            double totalLiabilities = companyDebt + merchantsCredits;

            // صافي المركز المالي = الأصول - الالتزامات
            double netFinancialPosition = totalAssets - totalLiabilities;

            // تحديث ViewModel (تأكد من تحديث خصائص UserStatsViewModel لتطابق هذه الأسماء إذا رغبت)
            var model = new UserStatsViewModel
            {
                Balance = posBalance,
                Indebt = companyDebt, // سيُرسل كرقم موجب الآن
                Cash = cashWallets,
                Bank = bankWallets,
                Debtors = merchantsDebts,
                Creditors = merchantsCredits,
                Inventory = netFinancialPosition // الناتج النهائي سيكون نفس الرقم القديم 77,012 ولكن بمنطق سليم
            };

            return View(model);
        }

        //public async Task<IActionResult> InventoryDetails()
        //{
        //    var userIdString = HttpContext.Session.GetString("UserID");
        //    if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
        //    int userId = int.Parse(userIdString);

        //    // 1. حساب الأرصدة (POS) - تم إضافة (double?) لضمان عمل الـ Sum حتى لو الجدول فارغ
        //    var balance = await _context.TblRegistrations
        //        .Where(r => r.UserFk == userId && r.IsActive == true)
        //        .SumAsync(r => (double?)r.Balance) ?? 0;

        //    // 2. حساب مديونية الوكيل (النقدية)
        //    var userAcc = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == userId && a.IsActive == true);
        //    double indebt = (double)(userAcc?.Indebtedness ?? 0) * -1;

        //    // 3. حساب محافظ الكاش والبنك
        //    double cash = await _context.TblServProvRegs
        //        .Where(p => p.UserFk == userId && p.ServProvFkNavigation.ProvTypeFk == 3 && p.IsActive == true)
        //        .SumAsync(p => (double?)p.Balance) ?? 0;

        //    // حساب المحافظ البنكية الانستا
        //    double bank = await _context.TblServProvRegs
        //        .Where(p => p.UserFk == userId && p.ServProvFkNavigation.ProvTypeFk == 4 && p.IsActive == true)
        //        .SumAsync(p => (double?)p.Balance) ?? 0;

        //    // 4. حساب المدينين والدائنين (من حسابات التجار التابعين لهذا الوكيل)
        //    // 4. حساب المدينين والدائنين (من حسابات التجار التابعين لهذا الوكيل)
        //    // فصلنا الكود ليكون أسرع وأوضح للمترجم
        //    var subUserIds = await _context.TblUsers
        //        .Where(u => u.ParentUser == userId)
        //        .Select(u => u.Id)
        //        .ToListAsync();

        //    var subUsersBalances = await _context.TblAccounts
        //        .Where(a => subUserIds.Contains(a.UserIdFk) && a.IsActive == true)
        //        .Select(a => (double)(a.Indebtedness ?? 0))
        //        .ToListAsync();

        //    double debtors = subUsersBalances.Where(b => b > 0).Sum(); // مبالغ على التجار
        //    double creditors = Math.Abs(subUsersBalances.Where(b => b < 0).Sum()); // مبالغ للتجار

        //    // 5. تطبيق المعادلة الجديدة للمركز المالي
        //    // المركز المالي = النقدية + المدينين + الأرصدة + الكاش + الانستا - الدائنين
        //    double finalInventory = indebt + debtors + balance + cash + bank - creditors;


        //    var model = new UserStatsViewModel
        //    {
        //        Balance = (double)balance,
        //        Indebt = indebt,
        //        Cash = cash,
        //        Bank = bank,
        //        Debtors = debtors,
        //        Creditors = creditors,
        //        Inventory = finalInventory
        //    };

        //    return View(model);
        //}

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}