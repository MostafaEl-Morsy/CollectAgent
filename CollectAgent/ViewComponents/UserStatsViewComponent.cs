using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CollectAgent.Models;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace CollectAgent.ViewComponents
{
    public class UserStatsViewComponent : ViewComponent
    {
        private readonly CollectAgentDBContext _db;

        public UserStatsViewComponent(CollectAgentDBContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new UserStatsViewModel();

            var claimsPrincipal = User as ClaimsPrincipal;
            var userIdClaim = claimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return View(model);
            }

            try
            {
                // النقدية بالخزينة
                var userDrawer = await _db.TblDrawers
                    .Where(d => d.UserFk == userId)
                    .FirstOrDefaultAsync();
                double drawer = userDrawer != null ? (double)(userDrawer.TtlAmntOwned ?? 0) : 0;

                // أرصدة الأجهزة (POS)
                double posBalance = await _db.TblRegistrations
                    .Where(r => r.UserFk == userId && r.IsActive == true)
                    .SumAsync(r => (double?)r.Balance) ?? 0;

                // محافظ الكاش
                double cashWallets = await _db.TblServProvRegs
                    .Where(p => p.UserFk == userId && p.ServProvFkNavigation.ProvTypeFk == 3 && p.IsActive == true)
                    .SumAsync(p => (double?)p.Balance) ?? 0;

                // المحافظ البنكية
                double bankWallets = await _db.TblServProvRegs
                    .Where(p => p.UserFk == userId && p.ServProvFkNavigation.ProvTypeFk == 4 && p.IsActive == true)
                    .SumAsync(p => (double?)p.Balance) ?? 0;

                // ديون/التزامات التابعين
                var subUserIds = await _db.TblUsers
                    .Where(u => u.ParentUser == userId)
                    .Select(u => u.Id)
                    .ToListAsync();

                var subUsersBalances = await _db.TblAccounts
                    .Where(a => subUserIds.Contains(a.UserIdFk) && a.IsActive == true)
                    .Select(a => (double)(a.Indebtedness ?? 0))
                    .ToListAsync();

                double merchantsDebts = subUsersBalances.Where(b => b > 0).Sum();
                double merchantsCredits = Math.Abs(subUsersBalances.Where(b => b < 0).Sum());

                // صافي المركز المالي
                double totalAssets = drawer + cashWallets + bankWallets + posBalance + merchantsDebts;
                double totalLiabilities = merchantsCredits;
                double netPosition = totalAssets - totalLiabilities;

                // تعيين القيم للـ ViewModel
                model.Balance = posBalance;     // ← أرصدة الأجهزة (كان ناقص)
                model.Drawer = drawer;
                model.Inventory = netPosition;
            }
            catch (Exception ex)
            {
                model.Balance = 0;
                model.Drawer = 0;
                model.Inventory = 0;
            }

            return View(model);
        }
    }
}