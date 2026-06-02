using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
// ... باقي الـ usings

namespace CollectAgent.Controllers
{
    // 1️⃣ حماية الكنترولر بالكامل، بحيث لا يدخله إلا من يملك صلاحية SuperAdmin
    // سنقوم بتعريف هذا الدور في الأسفل
    [Authorize(Roles = "SuperAdmin")]
    public class TblAdminsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblAdminsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: Admin Login
        [AllowAnonymous] // 2️⃣ السماح بالدخول لهذه الصفحة بدون تسجيل دخول
        public IActionResult AdminLogin()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: Admin Login
        [HttpPost]
        [AllowAnonymous] // 3️⃣ السماح بتنفيذ هذا الأكشن بدون تسجيل دخول
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminLogin([Bind("UserName,Password")] TblAdmin tblAdmin)
        {
            // التحقق اليدوي البسيط (يفضل استخدام View Model منفصل مثل LoginViewModel)
            if (string.IsNullOrEmpty(tblAdmin.UserName) || string.IsNullOrEmpty(tblAdmin.Password))
            {
                ModelState.AddModelError(string.Empty, "البيانات غير مكتملة.");
                return View(tblAdmin);
            }

            var admin = await _context.TblAdmins
                .FirstOrDefaultAsync(a => a.UserName == tblAdmin.UserName && a.Password == tblAdmin.Password && a.IsActive);

            if (admin != null)
            {
                // ✅ 4️⃣ إنشاء مطالبات الهوية (Claims) الخاصة بالأدمن
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                    new Claim(ClaimTypes.Name, admin.FullName),
                    // نستخدم هنا "SuperAdmin" لتمييزه عن المستخدمين العاديين (التجار)
                    // أو يمكنك استخدام رقم مميز مثل "999" إذا كنت تعتمد على الأرقام
                    new Claim(ClaimTypes.Role, "SuperAdmin"),
                    new Claim("IsAdmin", "True")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = false, // عادة الأدمن لا يفضل حفظ دخوله لفترة طويلة
                    ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
                };

                // ✅ 5️⃣ تنفيذ تسجيل الدخول الفعلي (إنشاء الكوكي)
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // ✅ 6️⃣ ملء الـ Session (ضروري لأن الـ Layout يعتمد عليه)
                HttpContext.Session.SetString("UserID", admin.Id.ToString());
                HttpContext.Session.SetString("FullName", admin.FullName);
                HttpContext.Session.SetString("UserRole", "SuperAdmin");
                // تعيين قيم افتراضية للأرصدة حتى لا تحدث مشاكل في العرض
                HttpContext.Session.SetString("Balance", "0.00");


                return RedirectToAction("AdminDashboard", "TblAdmins"); // أو توجيهه لـ Index الخاص بالـ Admins
            }
            else
            {
                ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");
            }

            return View(tblAdmin);
        }

        public IActionResult AdminLogout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("AdminLogin", "TblAdmins");
        }

        public IActionResult AdminDashboard()
        {
            return View();
        }

        // ... باقي دوال الـ CRUD (Index, Create, Edit...) تبقى كما هي
        // ولكنها الآن محمية تلقائياً بـ [Authorize(Roles = "SuperAdmin")] الموجودة أعلى الكلاس
    }
}