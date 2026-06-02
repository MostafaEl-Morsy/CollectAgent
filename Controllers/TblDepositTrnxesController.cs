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
    public class TblDepositTrnxesController : Controller
    {
        private readonly CollectAgentDBContext _context;
        private const int PageSize = 10; // عدّل الرقم كما تريد
        public TblDepositTrnxesController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // --- دالة مساعدة لجلب بيانات المستخدم الحالي ---
        private (int UserId, int UserType) GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userTypeClaim))
            {
                throw new InvalidOperationException("User claims are not available.");
            }

            return (int.Parse(userIdClaim), int.Parse(userTypeClaim));
        }

        // GET: TblDepositTrnxes
        // ✅ --- تم تحديث الدالة لتدعم البحث والتصفية ---
        public async Task<IActionResult> Index(string searchTerm, DateOnly? fromDate, DateOnly? toDate)
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                return StatusCode((int)HttpStatusCode.Forbidden, "ليس لديك الصلاحية لعرض هذه البيانات.");
            }

            // استدعاء الدالة المساعدة لبناء الاستعلام
            var query = GetFilteredDepositsQuery(searchTerm, fromDate, toDate);

            // معرفة العدد الإجمالي للنتائج لتحديد إذا كان هناك المزيد
            var totalCount = await query.CountAsync();
            ViewBag.HasMorePages = totalCount > PageSize;

            // جلب الصفحة الأولى فقط
            var initialDeposits = await query.Take(PageSize).ToListAsync();

            // إعادة قيم البحث إلى الواجهة للاحتفاظ بها في الحقول
            ViewData["CurrentSearchTerm"] = searchTerm;
            ViewData["CurrentFromDate"] = fromDate?.ToString("yyyy-MM-dd");
            ViewData["CurrentToDate"] = toDate?.ToString("yyyy-MM-dd");

            return View(initialDeposits);
        }

        // ✅ 2. إضافة Action جديدة لجلب البيانات عند التمرير
        [HttpGet]
        public async Task<IActionResult> LoadMoreDeposits(string searchTerm, DateOnly? fromDate, DateOnly? toDate, int page = 2)
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                return Forbid(); // منع الوصول
            }

            // استدعاء الدالة المساعدة لبناء الاستعلام
            var query = GetFilteredDepositsQuery(searchTerm, fromDate, toDate);

            // تطبيق الترحيل (تخطي الصفحات التي تم تحميلها بالفعل)
            var deposits = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

            if (!deposits.Any())
            {
                return NoContent(); // إرسال إشارة للـ JavaScript بعدم وجود المزيد من البيانات
            }

            // إرجاع البيانات كـ Partial View
            return PartialView("_DepositCardPartial", deposits);
        }

        // ✅ 3. دالة مساعدة لتجنب تكرار كود بناء الاستعلام
        private IQueryable<TblDepositTrnx> GetFilteredDepositsQuery(string searchTerm, DateOnly? fromDate, DateOnly? toDate)
        {
            var query = _context.TblDepositTrnxes
                .Include(t => t.Provider)
                .Include(t => t.StatusIdFkNavigation)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.Id) // ترتيب ثانوي لضمان ثبات النتائج
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(t => t.ReferenceCode.Contains(searchTerm) || t.Description.Contains(searchTerm));
            }
            if (fromDate.HasValue)
            {
                query = query.Where(t => t.Date >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                query = query.Where(t => t.Date <= toDate.Value);
            }

            return query;
        }
        // GET: TblDepositTrnxes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            // التحقق من صلاحية الجلسة
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return StatusCode((int)HttpStatusCode.Forbidden, "ليس لديك الصلاحية لعرض هذه البيانات.");
            }

            if (id == null)
            {
                return NotFound();
            }

            var tblDepositTrnx = await _context.TblDepositTrnxes
                .Include(t => t.DepositForNavigation)
                .Include(t => t.Provider)
                .Include(t => t.StatusIdFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDepositTrnx == null)
            {
                return NotFound();
            }

            return View(tblDepositTrnx);
        }

        // GET: TblDepositTrnxes/Create
        public IActionResult Create()
        {
            // التحقق من صلاحية الجلسة
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return StatusCode((int)HttpStatusCode.Forbidden, "ليس لديك الصلاحية لعرض هذه البيانات.");
            }

            ViewData["DepositFor"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            ViewData["ProviderId"] = new SelectList(_context.TblProviders, "Id", "ProviderCode");
            ViewData["StatusIdFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName");
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            return View();
        }

        // POST: TblDepositTrnxes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserIdFk,ReferenceCode,ProviderId,DepositAccNo,Date,DepositPhoto,DepositAmount,Le500,Le200,Le100,Le50,Le20,Le10,Le5,DepositFor,DepositType,StatusIdFk,Description")] TblDepositTrnx tblDepositTrnx)
        {
            // التحقق من صلاحية الجلسة
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return StatusCode((int)HttpStatusCode.Forbidden, "ليس لديك الصلاحية لعرض هذه البيانات.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(tblDepositTrnx);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["DepositFor"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDepositTrnx.DepositFor);
            ViewData["ProviderId"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblDepositTrnx.ProviderId);
            ViewData["StatusIdFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblDepositTrnx.StatusIdFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDepositTrnx.UserIdFk);
            return View(tblDepositTrnx);
        }

        // GET: TblDepositTrnxes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            // التحقق من صلاحية الجلسة
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return StatusCode((int)HttpStatusCode.Forbidden, "ليس لديك الصلاحية لعرض هذه البيانات.");
            }

            if (id == null)
            {
                return NotFound();
            }

            var tblDepositTrnx = await _context.TblDepositTrnxes.FindAsync(id);
            if (tblDepositTrnx == null)
            {
                return NotFound();
            }
            ViewData["DepositFor"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDepositTrnx.DepositFor);
            ViewData["ProviderId"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblDepositTrnx.ProviderId);
            ViewData["StatusIdFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblDepositTrnx.StatusIdFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDepositTrnx.UserIdFk);
            return View(tblDepositTrnx);
        }

        // POST: TblDepositTrnxes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserIdFk,ReferenceCode,ProviderId,DepositAccNo,Date,DepositPhoto,DepositAmount,Le500,Le200,Le100,Le50,Le20,Le10,Le5,DepositFor,DepositType,StatusIdFk,Description")] TblDepositTrnx tblDepositTrnx)
        {
            // التحقق من صلاحية الجلسة
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return StatusCode((int)HttpStatusCode.Forbidden, "ليس لديك الصلاحية لعرض هذه البيانات.");
            }

            if (id != tblDepositTrnx.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblDepositTrnx);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblDepositTrnxExists(tblDepositTrnx.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["DepositFor"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDepositTrnx.DepositFor);
            ViewData["ProviderId"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblDepositTrnx.ProviderId);
            ViewData["StatusIdFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblDepositTrnx.StatusIdFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDepositTrnx.UserIdFk);
            return View(tblDepositTrnx);
        }

        // GET: TblDepositTrnxes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            // التحقق من صلاحية الجلسة
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return StatusCode((int)HttpStatusCode.Forbidden, "ليس لديك الصلاحية لعرض هذه البيانات.");
            }

            if (id == null)
            {
                return NotFound();
            }

            var tblDepositTrnx = await _context.TblDepositTrnxes
                .Include(t => t.DepositForNavigation)
                .Include(t => t.Provider)
                .Include(t => t.StatusIdFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDepositTrnx == null)
            {
                return NotFound();
            }

            return View(tblDepositTrnx);
        }

        // POST: TblDepositTrnxes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblDepositTrnx = await _context.TblDepositTrnxes.FindAsync(id);
            if (tblDepositTrnx != null)
            {
                _context.TblDepositTrnxes.Remove(tblDepositTrnx);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblDepositTrnxExists(int id)
        {
            return _context.TblDepositTrnxes.Any(e => e.Id == id);
        }
    }
}