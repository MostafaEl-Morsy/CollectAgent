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
    public class TblCompaniesController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblCompaniesController(CollectAgentDBContext context)
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

        // GET: TblCompanies
        // تعرض القائمة للمدراء فقط (1-5)، أما الوكيل (6) يحول لصفحة تفاصيل شركته
        public async Task<IActionResult> Index()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            // إذا كان موظف أو تاجر (7-8)، ممنوع الدخول
            if (currentUserType > 6)
            {
                return RedirectToAction("Login", "Account");
            }

            // إذا كان المستخدم وكيل (6)، حوله مباشرة لتفاصيل شركته
            if (currentUserType == 6)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return RedirectToAction("Details"); // أو RedirectToAction لصفحة مخصصة
            }

            // المدراء (1-5): عرض كل الشركات
            return View(await _context.TblCompanies.ToListAsync());
        }

        // GET: TblCompanies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 6)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return RedirectToAction("Login", "Account");
            }
            int? idToView = null;

            if (currentUserType <= 5)
            {
                // المدير: يجب أن يرسل ID في الرابط، وإلا خطأ
                if (id == null) 
                {
                    TempData["ErrorMessage"] = "خطأ عام !!!";
                    return RedirectToAction("Index", "Home");
                };
                idToView = id;
            }
            else // currentUserType == 6
            {
                // الوكيل: يتجاهل الـ ID في الرابط ويستخدم شركته فقط
                if (currentUserCompanyId == null)
                {
                    TempData["ErrorMessage"] = "هذا المستخدم غير مرتبط بأي شركة.";
                    return RedirectToAction("Index", "Home");
                }
                idToView = currentUserCompanyId;
            }

            var tblCompany = await _context.TblCompanies
                .FirstOrDefaultAsync(m => m.Id == idToView);

            if (tblCompany == null)
            {
                TempData["ErrorMessage"] = "لا توجد شركة !!!";
                return RedirectToAction("Index", "Home");
            }

            return View(tblCompany);
        }

        // GET: TblCompanies/Create
        public IActionResult Create()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 5) // الوكيل لا ينشئ شركات
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لإضافة شركات.";
                return RedirectToAction("Index", "Home"); // أو RedirectToAction لصفحة مخصصة
            } 
            return View();
        }

        // POST: TblCompanies/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Logo,ContactNo,IsActive")] TblCompany tblCompany)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 5) return Forbid();

            if (ModelState.IsValid)
            {
                _context.Add(tblCompany);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة الشركة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(tblCompany);
        }

        // GET: TblCompanies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 6)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لتعديل الشركات.";
                return RedirectToAction("Index", "Home"); // أو RedirectToAction لصفحة مخصصة
            }

            int? idToEdit = null;
            if (currentUserType <= 5)
            {
                // المدير: نستخدم الـ ID القادم من الرابط (الذي ضغط عليه في Index)
                if (id == null)
                {
                    TempData["ErrorMessage"] = "لم يتم تحديد الشركة للتعديل.";
                    return RedirectToAction("Index", "Home");
                }
                idToEdit = id;
            }
            else // currentUserType == 6
            {
                // الوكيل: نستخدم ID شركته فقط بغض النظر عن الرابط
                if (currentUserCompanyId == null)
                {
                    TempData["ErrorMessage"] = "هذا المستخدم غير مرتبط بأي شركة.";
                    return RedirectToAction("Index", "Home");
                }
            }

            // جلب الشركة الخاصة بالمستخدم للتعديل
            var tblCompany = await _context.TblCompanies.FindAsync(idToEdit);

            if (tblCompany == null)
            {
                TempData["ErrorMessage"] = "لم يتم تحديد الشركة للتعديل.";
                return RedirectToAction("Index", "Home");
            }

            return View(tblCompany);
        }

        // POST: TblCompanies/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Logo,ContactNo,IsActive")] TblCompany tblCompany)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 6)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return RedirectToAction("Login", "Account");
            }

            // ✅ التحقق الأمني عند الحفظ
            if (currentUserType <= 5)
            {
                // المدير: يجب أن يطابق الـ ID المرسل مع الموديل
                if (id != tblCompany.Id)
                {
                    TempData["ErrorMessage"] = "لم يتم تحديد الشركة للتعديل.";
                    return RedirectToAction("Index", "Home");
                }
            }
            else // currentUserType == 6
            {
                // الوكيل: يجب التأكد أنه يحفظ بيانات شركته هو فقط
                if (currentUserCompanyId != tblCompany.Id) 
                {
                    TempData["ErrorMessage"] = "لم يتم تحديد الشركة للتعديل.";
                    return RedirectToAction("Index", "Home");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblCompany);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم حفظ التعديلات بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblCompanyExists(tblCompany.Id))
                    {
                        TempData["ErrorMessage"] = "لم يتم تحديد الشركة للتعديل.";
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        throw;
                    }
                }
                // المدير يعود للقائمة، الوكيل يعود للتفاصيل
                if (currentUserType <= 5) return RedirectToAction(nameof(Index));
                else return RedirectToAction(nameof(Details));
            }
            return View(tblCompany);
        }

        // GET: TblCompanies/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblCompany = await _context.TblCompanies
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblCompany == null)
            {
                return NotFound();
            }

            return View(tblCompany);
        }

        // POST: TblCompanies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblCompany = await _context.TblCompanies.FindAsync(id);
            if (tblCompany != null)
            {
                _context.TblCompanies.Remove(tblCompany);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblCompanyExists(int id)
        {
            return _context.TblCompanies.Any(e => e.Id == id);
        }
    }
}
