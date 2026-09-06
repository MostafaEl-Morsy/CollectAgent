using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CollectAgent.Controllers
{
    public class TblBlncTrnsActsController : Controller
    {
        private readonly CollectAgentDBContext _context;
        private const int PageSize = 15; // عدد العناصر في كل صفحة

        public TblBlncTrnsActsController(CollectAgentDBContext context)
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


        // --- دالة تطبيق صلاحيات العرض (Core Logic) ---
        private IQueryable<TblBlncTrnsAct> ApplyPermissionFilter(IQueryable<TblBlncTrnsAct> query, int userId, int userType, int? companyId)
        {
            // 1. الإداريون (أقل من 6): يرى كل التحويلات
            if (userType < 6)
            {
                return query;
            }

            // 2. الوكيل (يساوي 6): يرى تحويلاته + تحويلات المناديب في نفس الشركة
            if (userType == 6)
            {
                // إذا لم يكن لديه شركة، نعرض عملياته فقط كإجراء احترازي
                if (companyId == null)
                {
                    return query.Where(t => t.SendrUserId == userId || t.RecvrUserId == userId);
                }

                return query.Where(t =>
                    // (أ) عملياته الخاصة (سواء كان مرسل أو مستقبل)
                    (t.SendrUserId == userId || t.RecvrUserId == userId)
                    ||
                    // (ب) أو عمليات يكون فيها الطرف المرسل مندوب (نوع 7) ومن نفس الشركة
                    (t.SendrUser.CompanyFk == companyId && t.SendrUser.UserTypeFk == 7)
                    ||
                    // (ج) أو عمليات يكون فيها الطرف المستقبل مندوب (نوع 7) ومن نفس الشركة
                    (t.RecvrUser.CompanyFk == companyId && t.RecvrUser.UserTypeFk == 7)
                );
            }

            // 3. المندوب (يساوي 7): يرى تحويلاته فقط
            if (userType == 7)
            {
                return query.Where(t => t.SendrUserId == userId || t.RecvrUserId == userId);
            }

            // أي نوع آخر (أكبر من 7 أو غير معرف): يرى تحويلاته فقط (افتراضي)
            return query.Where(t => t.SendrUserId == userId || t.RecvrUserId == userId);
        }

        [Authorize(Roles = "1,2,3,4,5,6,7,8,9,10,11")]
        // GET: TblBlncTrnsActs
        public async Task<IActionResult> Index()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            // لا يوجد صلاحية لهذا المستخدم
            if (currentUserId == 0) return RedirectToAction("Login", "Account");
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات.";
                return RedirectToAction("Index", "Home");
            }

            // ✅ الحل المبسط: جلب حسابات (Registrations) المستخدم الحالي فقط
            var userAccounts = await _context.TblRegistrations
                .Where(r => r.UserFk == currentUserId)
                .Select(r => new { r.Id, r.AccNameNo })
                .ToListAsync();

            ViewBag.UserAccounts = new SelectList(userAccounts, "Id", "AccNameNo");

            // إعداد الاستعلام الأساسي 
            // ✅ ملاحظة: أضفنا SendrUser و RecvrUser لكي يعمل شرط الشركة والنوع
            var query = _context.TblBlncTrnsActs
                // ✅ إضافة ThenInclude لجلب بيانات مزود الخدمة المرتبط بالحساب
                .Include(t => t.RecvrReg).ThenInclude(r => r.ProviderFkNavigation)
                .Include(t => t.SendrReg).ThenInclude(r => r.ProviderFkNavigation)
                .Include(t => t.SendrUser)
                .Include(t => t.RecvrUser)
                .AsQueryable(); // تحويله لـ Queryable لنتمكن من إضافة الشروط

            // ✅ تطبيق فلتر الصلاحيات
            query = ApplyPermissionFilter(query, currentUserId, currentUserType, currentUserCompanyId);

            // إضافة الترتيب
            query = query.OrderByDescending(t => t.DateAndTime);

            // فلترة تاريخ اليوم
            var today = DateTime.Today;
            var initialTransactions = await query
                .Where(t => t.TransDate == DateOnly.FromDateTime(today))
                .Take(PageSize) // تحديد العدد هنا لتحسين الأداء
                .ToListAsync();

            // حساب الإجمالي لبيانات اليوم المعروضة
            // ملاحظة: إذا أردت إجمالي اليوم بالكامل (وليس الصفحة الأولى فقط) ستحتاج لاستعلام منفصل للإجمالي
            // لكن للكود الحالي، سنجمع ما تم جلبه:
            ViewBag.TotalSum = initialTransactions.Sum(t => t.BlncTrnsfr).ToString("N2", new CultureInfo("en-US"));
            ViewBag.CurrentUserId = currentUserId;
            return View(initialTransactions);
        }

        // POST: TblBlncTrnsActs/LoadTransactions
        // هذا هو الـ Action الجديد الذي سيتم استدعاؤه عبر AJAX
        [HttpPost]
        public async Task<IActionResult> LoadTransactions(DateTime fromDate, DateTime toDate, string searchQuery, int? senderRegId, int page = 1)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            // التحقق من الـ Login للأجاكس
            if (currentUserId == 0) return RedirectToAction("Login", "Home");
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات.";
                return RedirectToAction("Index", "Home");
            }

            // بناء الاستعلام الأساسي
            var query = _context.TblBlncTrnsActs
                // ✅ إضافة ThenInclude لجلب بيانات مزود الخدمة المرتبط بالحساب
                .Include(t => t.RecvrReg).ThenInclude(r => r.ProviderFkNavigation)
                .Include(t => t.SendrReg).ThenInclude(r => r.ProviderFkNavigation)
                .Include(t => t.SendrUser)
                .Include(t => t.RecvrUser)
                .AsQueryable();

            // ✅ تطبيق فلتر الصلاحيات أولاً
            query = ApplyPermissionFilter(query, currentUserId, currentUserType, currentUserCompanyId);

            // ✅ فلترة حسب الحساب المختار (رقم التسجيل / الحساب)
            if (senderRegId.HasValue && senderRegId.Value > 0)
            {
                // هات العمليات التي يكون فيها الحساب هو المرسل (صادر) أو المستقبل (وارد)
                query = query.Where(t => t.SendrRegId == senderRegId.Value || t.RecvrRegId == senderRegId.Value);
            }

            // تطبيق فلتر التاريخ
            // استخدام DateOnly للتأكد من المقارنة الصحيحة
            var fromDateOnly = DateOnly.FromDateTime(fromDate);
            var toDateOnly = DateOnly.FromDateTime(toDate);

            query = query.Where(t => t.TransDate >= fromDateOnly && t.TransDate <= toDateOnly);

            // تطبيق فلتر البحث
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(t =>
                    t.SendrReg.AccNameNo.Contains(searchQuery) ||
                    t.RecvrReg.AccNameNo.Contains(searchQuery) ||
                    t.ReferenceCode.Contains(searchQuery) ||
                    t.Description.Contains(searchQuery)
                );
            }

            // حساب الإجمالي الكلي للنتائج المتطابقة مع الفلتر
            var totalSum = await query.SumAsync(t => t.BlncTrnsfr);
            Response.Headers.Append("X-Total-Sum", totalSum.ToString("N2", new CultureInfo("en-US")));

            // تطبيق الترتيب وتقسيم الصفحات (Pagination)
            var transactions = await query
                .OrderByDescending(t => t.DateAndTime)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.CurrentUserId = currentUserId;

            return PartialView("_TransactionCardPartial", transactions);
        }
        private bool TblBlncTrnsActExists(int id)
        {
            return _context.TblBlncTrnsActs.Any(e => e.Id == id);
        }
    }
}
