// Controllers/AccountController.cs
using CollectAgent.Models;
using DatabaseAccess.Models; // تأكد من أن هذا هو الـ namespace الصحيح لموديلاتك
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // مهم جداً لإضافة FirstOrDefaultAsync
using System.Security.Claims;

public class AccountController : Controller
{
    private readonly CollectAgentDBContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(CollectAgentDBContext context, ILogger<AccountController> logger)
    {
        _context = context;
        _logger = logger; // وقم بتعيينه هنا
    }
    // GET: Account/Login
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. ✅ البحث عن المستخدم في قاعدة البيانات الحقيقية
        var user = await _context.TblUsers
            .FirstOrDefaultAsync(u => u.UserName == model.Username && u.IsActive == true);

        // 2. ✅ التحقق من المستخدم وكلمة المرور
        // !!! تنبيه أمني هام !!!
        // هذا الكود يقارن كلمة المرور كنص عادي وهو غير آمن.
        // يجب تحديثه لاستخدام نظام تشفير (Hashing) مثل BCrypt في أقرب فرصة.
        if (user == null || user.Password != model.Password)
        {
            ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة.");
            return View(model);
        }

        // 3. ✅ إنشاء الـ Claims من بيانات المستخدم الحقيقية
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            // لقد تم تصحيح اسم الحقل إلى UserTypeFk بناءً على موديل TblUser الذي أرسلته
            new Claim(ClaimTypes.Role, user.UserTypeFk.ToString())
        };

        // أضف CompanyFk فقط إذا كان له قيمة
        if (user.CompanyFk.HasValue)
        {
            // استخدم اسماً مخصصاً للـ Claim Type
            claims.Add(new Claim("CompanyId", user.CompanyFk.Value.ToString()));
        }

        // أضف BranchFk فقط إذا كان له قيمة
        if (user.BranchFk.HasValue)
        {
            // استخدم اسماً مخصصاً للـ Claim Type
            claims.Add(new Claim("BranchId", user.BranchFk.Value.ToString()));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        // 4. ✅ تسجيل دخول المستخدم وإنشاء الكوكي
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // 5. ✅ تخزين البيانات الإضافية في الـ Session من قاعدة البيانات الحقيقية
        // جلب البيانات المرتبطة بالمستخدم
        var userReg = await _context.TblRegistrations
        .Where(r => r.UserFk == user.Id && r.IsActive == true)
        .SumAsync(r => (double?)r.Balance) ?? 0;

        var userAcc = await _context.TblAccounts
            .FirstOrDefaultAsync(a => a.UserIdFk == user.Id && a.IsActive == true);

        // حساب مجاميع المحافظ (Wallets)
        double sumWallets = await _context.TblServProvRegs
        .Where(p => p.UserFk == user.Id && p.ServProvFkNavigation.ProvTypeFk == 3 && p.IsActive == true)
        .SumAsync(p => (double?)p.Balance) ?? 0;

        double sumBankWallets = await _context.TblServProvRegs
            .Where(p => p.UserFk == user.Id && p.ServProvFkNavigation.ProvTypeFk == 4 && p.IsActive == true)
            .SumAsync(p => (double?)p.Balance) ?? 0;

        // حساب العهدة (Inventory)
        // هذا مجرد مثال بناءً على الكود القديم، قد تحتاج لتعديل هذه المعادلة
        double companyDebt = (double)(userAcc?.Indebtedness ?? 0);
        //double indebt = (double)(userAcc?.Indebtedness ?? 0) * -1;
        double posBalance = userReg;

        // ✅ صافي رصيد الوكيل
        double agentNetBalance = posBalance + sumWallets + sumBankWallets - companyDebt;

        // تخزين القيم في الجلسة
        HttpContext.Session.SetString("UserID", user.Id.ToString());
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("Balance", posBalance.ToString("N2"));
        HttpContext.Session.SetString("CompanyDebt", companyDebt.ToString("N2"));
        HttpContext.Session.SetString("Inventory", agentNetBalance.ToString("N2"));
        HttpContext.Session.SetString("sumBankWallets", sumBankWallets.ToString("N2"));
        HttpContext.Session.SetString("sumWallets", sumWallets.ToString("N2"));

        return RedirectToAction("Index", "Home");
    }

    // Controllers/HomeController.cs
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}