using DatabaseAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CollectAgent.Controllers
{
    public class TblServProvRegsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblServProvRegsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // --- دالة مساعدة لجلب بيانات المستخدم الحالي ---
        private (int UserId, int UserTyp, int? CompanyId, int? BranchId) GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirstValue(ClaimTypes.Role);
            var companyIdClaim = User.FindFirstValue("CompanyId");
            var branchIdClaim = User.FindFirstValue("BranchId");

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userTypeClaim))
            {
                throw new InvalidOperationException("بيـانـات المـستخـدم غـير مـوجـودة");
            }

            int.TryParse(userIdClaim, out int userId);
            int.TryParse(userTypeClaim, out int userType);

            int? companyId = int.TryParse(companyIdClaim, out int cId) ? cId : (int?)null;
            int? branchId = int.TryParse(branchIdClaim, out int bId) ? bId : (int?)null;

            return (userId, userType, companyId, branchId);
        }

        // GET: TblServProvRegs
        public IActionResult Index()
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetWalletsData(int pageIndex = 1, int pageSize = 10)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();

            var query = _context.TblServProvRegs
                .Include(t => t.ServProvFkNavigation)
                .Include(t => t.UserFkNavigation)
                .AsQueryable();

            // المستخدم العادي يرى محافظه فقط، الأدمن يرى الجميع
            if (currentUserType >= 6)
            {
                query = query.Where(x => x.UserFk == currentUserId);
            }

            var items = await query
                .OrderByDescending(t => t.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return PartialView("_WalletListPartial", items);
        }

        public async Task<IActionResult> Details(int? id)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();
            if (id == null) return NotFound();

            var wallet = await _context.TblServProvRegs
                .Include(t => t.ServProvFkNavigation)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (wallet == null) return NotFound();

            if (currentUserType >= 6 && wallet.UserFk != currentUserId)
            {
                TempData["ErrorMessage"] = "عفواً، لا تملك صلاحية لعرض هذه المحفظة.";
                return RedirectToAction(nameof(Index));
            }
            return View(wallet);
        }

        public IActionResult Create()
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();

            var provider = _context.TblProviders.Where(p => p.ProvTypeFk == 3 || p.ProvTypeFk == 4).ToList();
            ViewData["ServProvFk"] = new SelectList(provider, "Id", "ProviderName");
            if (currentUserType >= 6)
            {
                var myUser = _context.TblUsers.Where(u => u.Id == currentUserId);
                ViewData["UserFk"] = new SelectList(myUser, "Id", "FullName", currentUserId);
            }
            else
            {
                ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "FullName");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserFk,ServNo,Code,ServProvFk,Balance,StartDate,IsActive")] TblServProvReg tblServProvReg)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();

            if (currentUserType >= 6) { tblServProvReg.UserFk = currentUserId; ModelState.Remove("UserFk"); }

            if (ModelState.IsValid)
            {
                _context.Add(tblServProvReg);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ServProvFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblServProvReg.ServProvFk);
            return View(tblServProvReg);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();
            if (id == null) return NotFound();

            var wallet = await _context.TblServProvRegs.FindAsync(id);
            if (wallet == null) return NotFound();

            if (currentUserType >= 6 && wallet.UserFk != currentUserId) return Forbid();

            ViewData["ServProvFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", wallet.ServProvFk);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", wallet.UserFk);
            return View(wallet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserFk,ServNo,Code,ServProvFk,Balance,StartDate,IsActive")] TblServProvReg tblServProvReg)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();
            if (id != tblServProvReg.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblServProvReg);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.TblServProvRegs.Any(e => e.Id == tblServProvReg.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(tblServProvReg);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();
            if (id == null) return NotFound();

            var wallet = await _context.TblServProvRegs.Include(t => t.UserFkNavigation).FirstOrDefaultAsync(m => m.Id == id);
            if (wallet == null) return NotFound();

            if (currentUserType >= 6 && wallet.UserFk != currentUserId) return Forbid();

            return View(wallet);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var wallet = await _context.TblServProvRegs.FindAsync(id);
            if (wallet != null)
            {
                _context.TblServProvRegs.Remove(wallet);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}