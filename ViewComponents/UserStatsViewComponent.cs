// ViewComponents/UserStatsViewComponent.cs
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CollectAgent.Models;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace CollectAgent.ViewComponents
{
    public class UserStatsViewComponent : ViewComponent
    {
        private readonly CollectAgentDBContext _db; // استخدمت اسم الـ DbContext الصحيح من ملفاتك
        //private readonly IHttpContextAccessor _httpContextAccessor;

        public UserStatsViewComponent(CollectAgentDBContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            //_httpContextAccessor = httpContextAccessor;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new UserStatsViewModel();
            //var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // ✅ الطريقة المضمونة لجلب المستخدم داخل ViewComponent
            var claimsPrincipal = User as ClaimsPrincipal;
            var userIdClaim = claimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                // لم يتم العثور على المستخدم، نعرض أصفار
                return View(model);
            }

            // --- هذا هو نفس منطق الحسابات من AccountController الخاص بك ---
            try
            {
                // ====== 1. الأصول ======

                // 1.1 رصيد الأجهزة (POS)
                double posBalance = await _db.TblRegistrations
                    .Where(r => r.UserFk == userId && r.IsActive == true)
                    .SumAsync(r => (double?)r.Balance) ?? 0;

                // 1.2 محافظ الكاش
                double cashWallets = await _db.TblServProvRegs
                    .Where(p => p.UserFk == userId && p.ServProvFkNavigation.ProvTypeFk == 3 && p.IsActive == true)
                    .SumAsync(p => (double?)p.Balance) ?? 0;

                // 1.3 المحافظ البنكية (انستا)
                double bankWallets = await _db.TblServProvRegs
                    .Where(p => p.UserFk == userId && p.ServProvFkNavigation.ProvTypeFk == 4 && p.IsActive == true)
                    .SumAsync(p => (double?)p.Balance) ?? 0;

                // ====== 2. الالتزامات ======

                // 2.1 مديونية الوكيل للشركة (رقم موجب = دين عليك)
                var userAcc = await _db.TblAccounts
                    .FirstOrDefaultAsync(a => a.UserIdFk == userId && a.IsActive == true);
                double companyDebt = (double)(userAcc?.Indebtedness ?? 0);
                // ✅ بدون ضرب في -1 ، نخليه موجب ونطرحه في المعادلة

                // ====== 3. صافي رصيد الوكيل (بدون التجار - للشريط العلوي فقط) ======
                // المعادلة: (رصيد الأجهزة + محافظ الكاش + المحافظ البنكية) - مديونية الشركة
                double agentNetBalance = posBalance + cashWallets + bankWallets - companyDebt;

                // ====== 4. تعيين القيم للـ ViewModel ======
                model.Balance = posBalance;
                model.Cash = cashWallets;
                model.Bank = bankWallets;
                model.Indebt = companyDebt;          // ✅ رقم موجب يمثل الدين
                model.Inventory = agentNetBalance;    // ✅ صافي الرصيد
            }
            catch (Exception ex)
            {
                // في حالة حدوث خطأ، نعرض أصفار
                model.Balance = 0;
                model.Inventory = 0;
                model.Cash = 0;
                model.Bank = 0;
                model.Indebt = 0;
            }

            return View(model);
        }
    }
}