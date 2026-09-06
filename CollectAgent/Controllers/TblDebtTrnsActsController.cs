using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using X.PagedList;

namespace CollectAgent.Controllers
{
    [Authorize] // ضمان أن كل الـ Actions في هذا الـ Controller تتطلب تسجيل الدخول
    public class TblDebtTrnsActsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblDebtTrnsActsController(CollectAgentDBContext context)
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

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return (0, 0, null, null);
            }

            int.TryParse(userIdClaim, out int userId);
            int.TryParse(userTypeClaim, out int userType);

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

        // GET: /TblDebtTrnsActs/CollectTransactIndex
        // صفحة عرض جرد التحصيل الرئيسية
        public IActionResult CollectTransactIndex()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            var viewModel = new CollectTransactViewModel
            {
                FromDate = DateOnly.FromDateTime(DateTime.Today),
                ToDate = DateOnly.FromDateTime(DateTime.Today)
            };

            return View(viewModel);
        }

        // POST: /TblDebtTrnsActs/LoadCollectionTransactions
        // Action لجلب البيانات باستخدام AJAX ودعم التحميل عند التمرير
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoadCollectionTransactions(DateTime fromDate, DateTime toDate, string searchQuery, int page = 1)
        {
            try
            {
                var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

                if (currentUserType > 7)
                {
                    TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                    return RedirectToAction("Index", "Home");
                }

                var userAcc = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == currentUserId);

                if (userAcc == null)
                {
                    return PartialView("_CollectionCardPartial", Enumerable.Empty<TblDebtTrnsAct>());
                }

                int collAccId = userAcc.Id;
                var toDateNextDay = toDate.Date.AddDays(1);
                const int pageSize = 10;

                // بناء الاستعلام
                var query = _context.TblDebtTrnsActs
                    .AsNoTracking()
                    .Include(t => t.AccidColFromFkNavigation)
                    .ThenInclude(acc => acc.UserIdFkNavigation)
                    .Where(t => t.AccidColToFk == collAccId && t.TransTypeId == 2);

                // تطبيق فلترة التاريخ
                query = query.Where(t => t.DateAndTime >= fromDate && t.DateAndTime < toDateNextDay);

                // تطبيق فلتر البحث
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    query = query.Where(item =>
                        (item.AccidColFromFkNavigation != null && item.AccidColFromFkNavigation.UserIdFkNavigation.FullName.Contains(searchQuery)) ||
                        item.ReferenceCode.Contains(searchQuery)
                    );
                }

                // حساب الإجمالي (للنقدي فقط بناءً على طلبك)
                if (page == 1)
                {
                    decimal sumColl = await query.SumAsync(item => (decimal?)item.AmntTrnsAct) ?? 0;
                    Response.Headers.Append("X-Total-Sum", sumColl.ToString("N2"));
                }

                // الترتيب وتطبيق التقسيم
                var transactions = await query.OrderByDescending(u => u.DateAndTime)
                                              .Skip((page - 1) * pageSize)
                                              .Take(pageSize)
                                              .ToListAsync();

                // التحقق إذا كان هناك المزيد من البيانات
                bool hasMoreData = await query.Skip(page * pageSize).AnyAsync();

                Response.Headers.Append("X-Has-More-Data", hasMoreData.ToString());

                return PartialView("_CollectionCardPartial", transactions);
            }
            catch (Exception)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, "حدث خطأ أثناء جلب البيانات.");
            }
        }

        // GET: TblDebtTrnsActs
        public async Task<IActionResult> BalanceSheet(string search, int page = 1)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            var account = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == currentUserId);
            if (account == null)
            {
                return View(new StaticPagedList<TblDebtTrnsAct>(new List<TblDebtTrnsAct>(), 1, 15, 0));
            }

            int accountID = account.Id;
            const int pageSize = 15;

            var query = _context.TblDebtTrnsActs
                .AsNoTracking()
                .Include(t => t.AccidColFromFkNavigation).ThenInclude(acc => acc.UserIdFkNavigation)
                .Include(t => t.AccidColToFkNavigation).ThenInclude(acc => acc.UserIdFkNavigation)
                .Include(t => t.TransType)
                .Include(t => t.ProvidFkNavigation)
                .Where(u => u.AccidColToFk == accountID || u.AccidColFromFk == accountID);

            if (!string.IsNullOrEmpty(search))
            {
                var searchTerm = search.ToLower();
                query = query.Where(t =>
                    (t.AccidColFromFkNavigation.UserIdFkNavigation.UserCode.ToLower().Contains(searchTerm)) ||
                    (t.AccidColToFkNavigation.UserIdFkNavigation.FullName.ToLower().Contains(searchTerm)) ||
                    (t.ProvidFkNavigation.ProviderName.ToLower().Contains(searchTerm)) ||
                    (t.ReferenceCode.ToLower().Contains(searchTerm)) ||
                    (t.Description.ToLower().Contains(searchTerm))
                );
            }

            var totalItemCount = await query.CountAsync();

            var itemsForCurrentPage = await query.OrderByDescending(u => u.DateAndTime)
                                                 .Skip((page - 1) * pageSize)
                                                 .Take(pageSize)
                                                 .ToListAsync();

            var pagedTransactions = new StaticPagedList<TblDebtTrnsAct>(itemsForCurrentPage, page, pageSize, totalItemCount);

            ViewBag.CurrentAccountId = accountID;
            ViewBag.SearchTerm = search;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_TransactionsList", pagedTransactions);
            }

            return View(pagedTransactions);
        }

        // GET: TblDebtTrnsActs/Index?merchantId=5
        public async Task<IActionResult> Index(int? merchantId, int page = 1)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (merchantId == null)
            {
                TempData["ErrorMessage"] = "لا يوجد حساب للتاجر !!!";
                return RedirectToAction("Index", "TblUsers", new { @mode = "oprate" });
            }

            var agentAccount = await _context.TblAccounts
                                             .Include(a => a.UserIdFkNavigation)
                                             .FirstOrDefaultAsync(a => a.UserIdFk == currentUserId);

            var merchantAccount = await _context.TblAccounts
                                                .Include(a => a.UserIdFkNavigation)
                                                .FirstOrDefaultAsync(a => a.UserIdFk == merchantId);

            if (agentAccount == null || merchantAccount == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على حساب مالي للوكيل أو التاجر.";
                return RedirectToAction("Index", "TblUsers", new { @mode = "oprate" });
            }

            ViewBag.MerchantName = merchantAccount.UserIdFkNavigation.UserCode;
            ViewBag.MerchantPhone = merchantAccount.UserIdFkNavigation.ContactNo;
            ViewBag.CurrentAgentId = agentAccount.Id;
            ViewBag.CurrentMerchantAccountId = merchantAccount.Id;


            // 5. بناء الاستعلام
            var query = _context.TblDebtTrnsActs
                .AsNoTracking()
                .Include(t => t.TransType)
                .Include(t => t.ProvidFkNavigation)
                .Where(t =>
                    (t.AccidColFromFk == agentAccount.Id && t.AccidColToFk == merchantAccount.Id) ||
                    (t.AccidColFromFk == merchantAccount.Id && t.AccidColToFk == agentAccount.Id)
                );

            // === [الحل النهائي لحساب الصافي بناءً على TransType] ===

            // أرقام العمليات الواردة للوكيل (موجب)
            var incomingTypes = new List<int> { 2, 5, 6 };
            // أرقام العمليات الصادرة من الوكيل (سالب)
            var outgoingTypes = new List<int> { 1, 3, 4 };

            // إجمالي الوارد (نجمع الكاش + الرصيد معاً كقيم مطلقة)
            var totalIncoming = await query.Where(t => incomingTypes.Contains(t.TransTypeId))
                .SumAsync(t => (decimal?)Math.Abs(t.AmntTrnsAct) + (decimal?)Math.Abs(t.BlncTrnsAct ?? 0)) ?? 0;

            // إجمالي الصادر (نجمع الكاش + الرصيد معاً كقيم مطلقة)
            var totalOutgoing = await query.Where(t => outgoingTypes.Contains(t.TransTypeId))
                .SumAsync(t => (decimal?)Math.Abs(t.AmntTrnsAct) + (decimal?)Math.Abs(t.BlncTrnsAct ?? 0)) ?? 0;

            // الصافي النهائي
            ViewBag.NetBalance = totalIncoming - totalOutgoing;
            // =========================================================

            // 6. الترتيب والتقسيم للصفحات

            int pageSize = 20;
            var totalItemCount = await query.CountAsync();

            var items = await query.OrderByDescending(t => t.DateAndTime)
                                   .Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            var pagedList = new StaticPagedList<TblDebtTrnsAct>(items, page, pageSize, totalItemCount);

            return View(pagedList);
        }

        // GET: TblDebtTrnsActs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (id == null)
            {
                return NotFound();
            }

            var tblDebtTrnsAct = await _context.TblDebtTrnsActs
                .Include(t => t.AccidColFromFkNavigation)
                .Include(t => t.AccidColToFkNavigation)
                .Include(t => t.ProvidFkNavigation)
                .Include(t => t.TransType)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDebtTrnsAct == null)
            {
                return NotFound();
            }

            return View(tblDebtTrnsAct);
        }

        // GET: TblDebtTrnsActs/Create
        public IActionResult Create()
        {
            ViewData["AccidColFromFk"] = new SelectList(_context.TblAccounts, "Id", "Id");
            ViewData["AccidColToFk"] = new SelectList(_context.TblAccounts, "Id", "Id");
            ViewData["ProvidFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode");
            ViewData["TransTypeId"] = new SelectList(_context.TblTransTypes, "Id", "TransTypeName");
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            return View();
        }

        // POST: TblDebtTrnsActs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserFk,ReferenceCode,AccidColToFk,AccidColFromFk,AmntTrnsAct,BlncTrnsAct,ColToDbtBfr,ColToDbtAftr,ColFrmDbtBefr,ColfrmDebtAfter,TransDate,DateAndTime,ProvidFk,TransTypeId,Description")] TblDebtTrnsAct tblDebtTrnsAct)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                _context.Add(tblDebtTrnsAct);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AccidColFromFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblDebtTrnsAct.AccidColFromFk);
            ViewData["AccidColToFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblDebtTrnsAct.AccidColToFk);
            ViewData["ProvidFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblDebtTrnsAct.ProvidFk);
            ViewData["TransTypeId"] = new SelectList(_context.TblTransTypes, "Id", "TransTypeName", tblDebtTrnsAct.TransTypeId);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDebtTrnsAct.UserFk);
            return View(tblDebtTrnsAct);
        }

        // GET: TblDebtTrnsActs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }
            if (id == null)
            {
                return NotFound();
            }

            var tblDebtTrnsAct = await _context.TblDebtTrnsActs.FindAsync(id);
            if (tblDebtTrnsAct == null)
            {
                return NotFound();
            }
            ViewData["AccidColFromFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblDebtTrnsAct.AccidColFromFk);
            ViewData["AccidColToFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblDebtTrnsAct.AccidColToFk);
            ViewData["ProvidFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblDebtTrnsAct.ProvidFk);
            ViewData["TransTypeId"] = new SelectList(_context.TblTransTypes, "Id", "TransTypeName", tblDebtTrnsAct.TransTypeId);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDebtTrnsAct.UserFk);
            return View(tblDebtTrnsAct);
        }

        // POST: TblDebtTrnsActs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserFk,ReferenceCode,AccidColToFk,AccidColFromFk,AmntTrnsAct,BlncTrnsAct,ColToDbtBfr,ColToDbtAftr,ColFrmDbtBefr,ColfrmDebtAfter,TransDate,DateAndTime,ProvidFk,TransTypeId,Description")] TblDebtTrnsAct tblDebtTrnsAct)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }
            if (id != tblDebtTrnsAct.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblDebtTrnsAct);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblDebtTrnsActExists(tblDebtTrnsAct.Id))
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
            ViewData["AccidColFromFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblDebtTrnsAct.AccidColFromFk);
            ViewData["AccidColToFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblDebtTrnsAct.AccidColToFk);
            ViewData["ProvidFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblDebtTrnsAct.ProvidFk);
            ViewData["TransTypeId"] = new SelectList(_context.TblTransTypes, "Id", "TransTypeName", tblDebtTrnsAct.TransTypeId);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDebtTrnsAct.UserFk);
            return View(tblDebtTrnsAct);
        }

        // GET: TblDebtTrnsActs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }
            if (id == null)
            {
                return NotFound();
            }

            var tblDebtTrnsAct = await _context.TblDebtTrnsActs
                .Include(t => t.AccidColFromFkNavigation)
                .Include(t => t.AccidColToFkNavigation)
                .Include(t => t.ProvidFkNavigation)
                .Include(t => t.TransType)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDebtTrnsAct == null)
            {
                return NotFound();
            }

            return View(tblDebtTrnsAct);
        }

        // POST: TblDebtTrnsActs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblDebtTrnsAct = await _context.TblDebtTrnsActs.FindAsync(id);
            if (tblDebtTrnsAct != null)
            {
                _context.TblDebtTrnsActs.Remove(tblDebtTrnsAct);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblDebtTrnsActExists(int id)
        {
            return _context.TblDebtTrnsActs.Any(e => e.Id == id);
        }
    }
}