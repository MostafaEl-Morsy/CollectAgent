using DatabaseAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CollectAgent.Controllers
{
    public class TblDrawersController : Controller
    {
        private readonly CollectAgentDBContext _context;
        private const int PageSize = 10; // عدد الكروت في كل تحميل

        public TblDrawersController(CollectAgentDBContext context)
        {
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

        // GET: TblDrawers
        public async Task<IActionResult> Index(string searchTerm)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return StatusCode((int)HttpStatusCode.Forbidden, "ليس لديك الصلاحية لعرض هذه البيانات."); // أو RedirectToAction لصفحة مخصصة
            }
            var query = GetDrawersQuery(searchTerm);
            //var initialData = await query.Take(PageSize).ToListAsync();

            //ViewBag.HasMorePages = await query.CountAsync() > PageSize;
            //ViewData["CurrentSearch"] = searchTerm;
            // حساب العدد الإجمالي للتحقق من وجود المزيد
            var totalCount = await query.CountAsync();
            ViewBag.HasMorePages = totalCount > PageSize;
            ViewData["CurrentSearch"] = searchTerm;

            var initialData = await query.Take(PageSize).ToListAsync();
            return View(initialData);
        }

        // دالة جلب المزيد من البيانات عبر AJAX
        [HttpGet]
        public async Task<IActionResult> LoadMoreDrawers(string searchTerm, int page = 2)
        {
            var query = GetDrawersQuery(searchTerm);
            var drawers = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

            if (!drawers.Any()) return NoContent();

            return PartialView("_DrawerCardPartial", drawers);
        }

        // ✅ دالة بناء الاستعلام مع تطبيق الصلاحيات
        private IQueryable<TblDrawer> GetDrawersQuery(string searchTerm)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            var query = _context.TblDrawers
                .Include(d => d.UserFkNavigation) // لجلب اسم المستخدم
                .OrderByDescending(d => d.TtlAmntOwned)
                .AsQueryable();

            // --- 1. الإداريون (UserType < 6): يرون كل شيء ---
            // --- 1. الإداريون (UserType < 6): يرون كل شيء ---
            if (currentUserType < 6)
            {
                // لا قيود
            }
            // --- 2. الوكلاء (UserType = 6): يرون خزائن المستخدمين في نفس الشركة ---
            else if (currentUserType == 6 && currentUserCompanyId.HasValue)
            {
                query = query.Where(d => d.UserFkNavigation.CompanyFk == currentUserCompanyId.Value);
            }
            // --- 3. المندوبون (UserType = 7): يرون خزائنهم الشخصية فقط ---
            else if (currentUserType == 7)
            {
                query = query.Where(d => d.UserFk == currentUserId);
            }
            else
            {
                // حجب البيانات لأي نوع آخر
                query = query.Where(d => false);
            }

            // البحث
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(d => d.UserFkNavigation.UserCode.Contains(searchTerm) ||
                                         d.UserFkNavigation.FullName.Contains(searchTerm) ||
                                         d.UserFkNavigation.ContactNo.Contains(searchTerm));
            }

            // الترتيب: الأكبر رصيداً يظهر أولاً
            query = query.OrderByDescending(d => d.TtlAmntOwned);

            return query;
        }

        // GET: TblDrawers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (id == null)
            {
                return NotFound();
            }

            var tblDrawer = await _context.TblDrawers
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDrawer == null)
            {
                return NotFound();
            }

            return View(tblDrawer);
        }

        // GET: TblDrawers/Create
        public IActionResult Create()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            return View();
        }

        // POST: TblDrawers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Code,UserFk,Le500,Le200,Le100,Le50,Le20,Le10,Le5,TtlAmntOwned,StartDate,IsActive")] TblDrawer tblDrawer)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                _context.Add(tblDrawer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDrawer.UserFk);
            return View(tblDrawer);
        }

        // GET: TblDrawers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (id == null)
            {
                TempData["ErrorMessage"] = " بيانات غير موجودة !!!";
                return RedirectToAction("Index", "Home");
            }

            var tblDrawer = await _context.TblDrawers.FindAsync(id);
            if (tblDrawer == null)
            {
                TempData["ErrorMessage"] = " بيانات غير موجودة !!!";
                return RedirectToAction("Index", "Home");
            }
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDrawer.UserFk);
            return View(tblDrawer);
        }

        // POST: TblDrawers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Code,UserFk,Le500,Le200,Le100,Le50,Le20,Le10,Le5,TtlAmntOwned,StartDate,IsActive")] TblDrawer tblDrawer)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (id != tblDrawer.Id)
            {
                TempData["ErrorMessage"] = " بيانات غير موجودة !!!";
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblDrawer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblDrawerExists(tblDrawer.Id))
                    {
                        TempData["ErrorMessage"] = " بيانات غير موجودة !!!";
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDrawer.UserFk);
            return View(tblDrawer);
        }

        // GET: TblDrawers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDrawer = await _context.TblDrawers
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDrawer == null)
            {
                return NotFound();
            }

            return View(tblDrawer);
        }

        // POST: TblDrawers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblDrawer = await _context.TblDrawers.FindAsync(id);
            if (tblDrawer != null)
            {
                _context.TblDrawers.Remove(tblDrawer);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblDrawerExists(int id)
        {
            return _context.TblDrawers.Any(e => e.Id == id);
        }
    }
}
