using DatabaseAccess.Models;
using CollectAgent.Models;
using CollectAgent.ViewModels;
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
    public class TblUsersController : Controller
    {
        private readonly CollectAgentDBContext _context;
        private readonly UserService _userService;
        private const int PageSize = 20;

        public TblUsersController(CollectAgentDBContext context)
        {
            _context = context;
            _userService = new UserService(_context);
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

        // ✅✅✅ دالة الفلترة الموحدة والذكية ✅✅✅
        private IQueryable<TblUser> GetFilteredQuery(string mode)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, _) = GetCurrentUserInfo();
            IQueryable<TblUser> query = _context.TblUsers.AsQueryable();

            // --- فلترة أساسية بناءً على صلاحيات المستخدم الحالي (تطبق دائماً) ---
            if (currentUserType == 7) // المندوب يرى فقط من يتبعونه
            {
                // المندوب يرى فقط من يتبعونه مباشرةً (وهم التجار).
                // هذا الشرط يطبق على كل الأوضاع (admin و oprate).
                query = query.Where(u => u.ParentUser == currentUserId);
            }
            else if (currentUserType == 6) // الوكيل يرى فقط من في شركته
            {
                query = query.Where(u => u.CompanyFk == currentUserCompanyId);
            }
            // الإدارة (أقل من 6) يرون الجميع، لذلك لا يوجد شرط هنا.

            // --- فلترة ثانوية بناءً على وضع العرض (mode) ---
            if (mode == "oprate")
            {
                // في وضع العمليات، نعرض التجار النشطين فقط
                query = query.Where(u => u.IsActive == true);
            }

            return query;
        }


        // GET: TblUsers
        public async Task<IActionResult> Index(string mode = "admin")
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            // استخدام الدالة الموحدة مع تمرير الـ mode
            IQueryable<TblUser> query = GetFilteredQuery(mode);

            int totalCount = await query.CountAsync();
            ViewBag.HasMorePages = totalCount > PageSize;
            ViewBag.DisplayMode = mode;

            var users = await query
                .Include(t => t.UserTypeFkNavigation)
                .OrderBy(u => u.StartDate)
                .Take(PageSize)
                .ToListAsync();

            // سنستخدم View واحدة اسمها Index.cshtml
            return View(users);
        }


        // GET: TblUsers/Subscriptions
        public IActionResult Subscriptions()
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();

            // التحقق من الصلاحيات (الإدارة فقط أو حسب الحاجة)
            if (currentUserType > 6) // مثال: السماح فقط للأدمن والوكلاء
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات";
                return RedirectToAction("Index", "Home");
            }

            // تعيين القيم الافتراضية للفلاتر للعرض في الـ View
            ViewBag.FromDate = DateTime.Today.ToString("yyyy-MM-dd");
            ViewBag.ToDate = DateTime.Today.AddMonths(1).ToString("yyyy-MM-dd");

            return View();
        }

        // AJAX: جلب بيانات الاشتراكات (Infinite Scroll + Filtering)
        public async Task<IActionResult> GetSubscriptionUsers(int page = 1, string searchTerm = "", string fromDate = "", string toDate = "")
        {
            var (currentUserId, currentUserType, currentUserCompanyId, _) = GetCurrentUserInfo();

            IQueryable<TblUser> query = _context.TblUsers.AsQueryable();

            // 1. الفلترة الأساسية للصلاحيات (نسخ منطق GetFilteredQuery)
            if (currentUserType == 6) query = query.Where(u => u.CompanyFk == currentUserCompanyId);
            else if (currentUserType == 7) query = query.Where(u => u.ParentUser == currentUserId);

            // 2. فلترة البحث (بالاسم أو الرقم)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(u => u.FullName.Contains(searchTerm) || u.ContactNo.Contains(searchTerm) || u.UserCode.Contains(searchTerm));
            }

            // 3. فلترة التواريخ (تاريخ انتهاء الاشتراك)
            if (DateTime.TryParse(fromDate, out DateTime dtFrom))
            {
                // نحول التاريخ إلى DateOnly إذا كان الحقل في قاعدة البيانات DateOnly
                var dFrom = DateOnly.FromDateTime(dtFrom);
                query = query.Where(u => u.SubEndDate >= dFrom);
            }

            if (DateTime.TryParse(toDate, out DateTime dtTo))
            {
                var dTo = DateOnly.FromDateTime(dtTo);
                query = query.Where(u => u.SubEndDate <= dTo);
            }

            // الترتيب حسب تاريخ الانتهاء الأقرب
            var users = await query
                .Include(u => u.UserTypeFkNavigation) // تأكد من وجود علاقة BalanceMode إذا كنت ستعرضها
                .OrderBy(u => u.SubEndDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return PartialView("_SubscriptionCardsPartial", users);
        }

        // Action لتغيير الحالة (تفعيل/إيقاف)
        [HttpPost]
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var user = await _context.TblUsers.FindAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات";
                return RedirectToAction("Index", "Home");
            }

            // 1. اعكس الحالة مرة واحدة واحفظ الحالة الجديدة في متغير
            user.IsActive = !user.IsActive;
            bool newState = user.IsActive; // سنستخدم هذا المتغير للجميع

            // إذا تم التفعيل، وتاريخ الانتهاء قديم، ربما تريد تجديده تلقائياً لشهر قادم؟
            // هذا اختياري:
            if (user.IsActive == true && user.SubEndDate < DateOnly.FromDateTime(DateTime.Today))
            {
                user.SubEndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1));
            }

            // 2. جهّز قائمة فارغة للمستخدمين المتأثرين
            List<TblUser> affectedUsers = new List<TblUser>();

            // 3. املأ القائمة بناءً على نوع المستخدم (بدون تكرار)
            if (user.UserTypeFk == 6) // وكيل
            {
                affectedUsers = await _context.TblUsers
                    .Where(u => u.CompanyFk == user.CompanyFk && u.Id != user.Id)
                    .ToListAsync();
            }
            else if (user.UserTypeFk == 7) // مندوب
            {
                affectedUsers = await _context.TblUsers
                    .Where(u => u.ParentUser == user.Id)
                    .ToListAsync();
            }

            // 4. قم بتطبيق الحالة الجديدة على كل المستخدمين في القائمة
            if (affectedUsers.Any())
            {
                affectedUsers.ForEach(subUser => subUser.IsActive = newState);
            }

            // 5. احفظ كل التغييرات دفعة واحدة
            await _context.SaveChangesAsync();

            return Json(new { success = true, newState = user.IsActive, newDate = user.SubEndDate?.ToString("yyyy/MM/dd") });
        }

        // هذا الـ Action سيتم استدعاؤه بواسطة AJAX
        // ✅ Action AJAX موحد
        // GET: TblUsers/GetUsers?page=2
        public async Task<IActionResult> GetUsers(int page = 1, string mode = "admin")
        {
            var query = GetFilteredQuery(mode); // استخدام نفس الدالة الموحدة

            var users = await query
                .Include(t => t.UserTypeFkNavigation)
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.DisplayMode = mode;
            return PartialView("_UserCardsPartial", users);
        }

        // GET: TblUsers/SearchUsers?searchTerm=...
        public async Task<IActionResult> SearchUsers(string searchTerm, string mode = "admin")
        {
            var query = GetFilteredQuery(mode); // ابدأ بالفلترة الصحيحة

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(u => u.FullName.Contains(searchTerm) || u.ContactNo.Contains(searchTerm) || u.UserCode.Contains(searchTerm));
            }
            else
            {
                return PartialView("_UserCardsPartial", new List<TblUser>());
            }

            var users = await query
                .Include(t => t.UserTypeFkNavigation)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.DisplayMode = mode;
            return PartialView("_UserCardsPartial", users);
        }

        // GET: Users/AddUser
        // [AuthorizeUser(Roles="Admin,Manager")] // طريقة أفضل باستخدام Action Filter مخصص
        public IActionResult AddUser()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "عفوا ليس لك صلاحية عرض البيانات !!!";
                return RedirectToAction("Index", "Home"); // أو RedirectToAction لصفحة مخصصة
            }
            ViewBag.CurrentUserType = currentUserType;
            var model = new AddUserViewModel();

            // سيقوم بتعيين تاريخ نهاية الاشتراك ليكون بعد شهر واحد بالضبط من تاريخ اليوم
            model.SubEndDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(1));

            PopulateDropdownsForAdd(model, currentUserType);

            return View(model);
        }

        // POST: Users/AddUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddUser(AddUserViewModel model)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!!";
                return RedirectToAction("Index", "Home"); // أو RedirectToAction لصفحة مخصصة
            }

            // هذا التحقق ينطبق فقط عند إضافة مستخدم جديد له اسم مستخدم فعلي (وليس تاجر)
            if (currentUserType < 7)
            {
                // تحقق مما إذا كان اسم المستخدم موجوداً بالفعل (بحث غير حساس لحالة الأحرف)
                bool isUsernameTaken = await _context.TblUsers
                    .AnyAsync(u => u.UserName.ToLower() == model.UserName.ToLower());

                if (isUsernameTaken)
                {
                    // أضف خطأ إلى ModelState. هذا سيجعل ModelState.IsValid تساوي false
                    ModelState.AddModelError("UserName", "اسم المستخدم هذا موجود مسبقاً، يرجى اختيار اسم آخر.");
                }
            }

            TblUser agentUser = await _context.TblUsers.FindAsync(currentUserId);

            if (currentUserType == 7)
            {
                // 1. قم بإزالة التحقق من الحقول التي ستكون فارغة
                ModelState.Remove("UserName");
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
                // يمكنك أيضاً إضافة الحقول الأخرى غير الموجودة للتاجر لضمان عدم حدوث مشاكل مستقبلية
                ModelState.Remove("UserTypeFk"); // لأننا نحددها يدوياً
                ModelState.Remove("SubEndDate"); // غير موجودة في نموذج التاجر

                // 2. عيّن قيم افتراضية للحقول التي ستستخدمها في طبقة الخدمة
                // يمكن استخدام رقم هاتف التاجر أو أي قيمة فريدة أخرى مؤقتاً
                model.UserName = "Null";
                model.Password = "Null"; // يجب أن تكون كلمة سر قوية ومؤقتة
                model.UserTypeFk = 8; // تعيين صلاحية المستخدم الافتراضية
            }
            else
            {
                ModelState.Remove("Indebtedness");
                // إذا كان المستخدم ليس من النوع 7، قم بتعيين مزود الخدمة بناءً على المستخدم الحالي
                //ModelState.Remove("Code"); // لأننا نستخدم القيمة الافتراضية
            }
            ModelState.Remove("Districts");
            ModelState.Remove("Governates");
            ModelState.Remove("UserTypes");
            ModelState.Remove("Representative"); // لأننا نستخدم القيمة الافتراضية
            ModelState.Remove("ContactNo"); // لأننا نستخدم القيمة الافتراضية
            ModelState.Remove("Address"); // لأننا نستخدم القيمة الافتراضية
            ModelState.Remove("Street"); // لأننا نستخدم القيمة الافتراضية
            ModelState.Remove("Distrect_FK"); // لأننا نستخدم القيمة الافتراضية
            ModelState.Remove("Governate_FK"); // لأننا نستخدم القيمة الافتراضية
            ModelState.Remove("Indebtedness"); // لأننا نستخدم القيمة الافتراضية
            ModelState.Remove("UserTypeFk"); // لأننا نستخدم القيمة الافتراضية

            // تعيين بعض القيم الافتراضية بناءً على نوع المستخدم الحالي
            model.Representative = currentUserId; // تعيين المستخدم الحالي كممثل
            model.Code = model.Code ?? model.FullName; // تعيين كود المستخدم بنفس اسم المحل
            model.IsActive = true; // تعيين المستخدم الجديد كمفعل بشكل افتراضي
            model.ApprvDate = DateOnly.FromDateTime(DateTime.Now); // تعيين تاريخ الموافقة ليكون اليوم
            model.SubEndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
            model.CompanyFk = currentUserType < 6 ? model.CompanyFk : agentUser.CompanyFk;
            model.BranchFk = currentUserType < 6 ? 1 : agentUser.BranchFk;
            model.Street = model.Street ?? "Null";

            // التحقق من صحة النموذج
            if (ModelState.IsValid)
            {
                bool success = _userService.CreateUserAndRelatedEntities(model, currentUserType);

                if (success)
                {
                    TempData["Success"] = "تمت إضافة المستخدم بنجاح!";
                    // استخدام نمط PRG: أعد التوجيه إلى صفحة أخرى بعد النجاح
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", "حدث خطأ غير متوقع أثناء حفظ البيانات. يرجى المحاولة مرة أخرى.");
                }
            }
            // أضف هذا السطر لتجنب الخطأ عند إعادة عرض الصفحة
            ViewBag.CurrentUserType = currentUserType;

            // إذا فشل التحقق أو فشلت عملية الحفظ، أعد ملء القوائم وأعد عرض النموذج مع الأخطاء
            PopulateDropdownsForAdd(model, currentUserType);
            return View(model);
        }

        public JsonResult GetDistrictsByGovernateId(int governateId)
        {
            var districts = _context.TblDistricts
                              .Where(d => d.GovernateFk == governateId)
                              .Select(d => new { Value = d.Id, Text = d.DistrictName })
                              .ToList();

            return Json(districts); // <-- فقط قم بإرجاع الكائن مباشرة
        }

        // AJAX endpoint to get branches by company
        public JsonResult GetBranchesByCompanyId(int companyId)
        {
            var branches = _context.TblBranches
                .Where(b => b.CompanyFk == companyId && b.IsActive)
                .Select(b => new { Value = b.Id, Text = b.Name })
                .ToList();
            return Json(branches);
        }

        [HttpPost]
        public JsonResult CheckUsernameAvailability(string username)
        {
            bool exists = false;
            exists = _context.TblUsers.Any(u => u.UserName.ToLower() == username.ToLower());

            return Json(new { exists = exists });
        }

        // GET: TblUsers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات";
                return RedirectToAction("Index", "Home"); // أو RedirectToAction لصفحة مخصصة
            }

            if (id == null)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات";
                return RedirectToAction(nameof(Index));
            }

            var tblUser = await _context.TblUsers
                .Include(t => t.BranchFkNavigation)
                .Include(t => t.CompanyFkNavigation)
                .Include(t => t.DirctionFkNavigation)
                .Include(t => t.DistrectFkNavigation)
                .Include(t => t.GovernateFkNavigation)
                .Include(t => t.UserTypeFkNavigation)
                // **ملاحظة:** إذا أردت عرض اسم المستخدم الأب (ParentUser) بدلاً من معرّفه (ID)،
                // ستحتاج إلى إضافة تضمين لخاصية تنقل ParentUserNavigation هنا،
                // بشرط أن تكون معرفة في الـ Model الخاص بـ TblUser.
                // مثال (إذا كانت الخاصية موجودة):
                // .Include(t => t.ParentUserNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tblUser == null)
            {
                return NotFound();
            }

            return View(tblUser);
        }

        // GET: TblUsers/Create
        public IActionResult Create()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            ViewData["BranchFk"] = new SelectList(_context.TblBranches, "Id", "Name");
            ViewData["CompanyFk"] = new SelectList(_context.TblCompanies, "Id", "Name");
            ViewData["DirctionFk"] = new SelectList(_context.TblDirctions, "Id", "RouteName");
            ViewData["DistrectFk"] = new SelectList(_context.TblDistricts, "Id", "DistrictName");
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName");
            ViewData["UserTypeFk"] = new SelectList(_context.TblUserTypes, "Id", "UserTypeName");
            return View();
        }

        // POST: TblUsers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FullName,ContactNo,UserName,Password,Photo,Address,Street,DistrectFk,GovernateFk,Naid,Email,StartDate,SubEndDate,UserTypeFk,DirctionFk,Priority,CompanyFk,BranchFk,ParentUser,UserCode,IsActive")] TblUser tblUser)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            if (ModelState.IsValid)
            {
                _context.Add(tblUser);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BranchFk"] = new SelectList(_context.TblBranches, "Id", "Name", tblUser.BranchFk);
            ViewData["CompanyFk"] = new SelectList(_context.TblCompanies, "Id", "Name", tblUser.CompanyFk);
            ViewData["DirctionFk"] = new SelectList(_context.TblDirctions, "Id", "RouteName", tblUser.DirctionFk);
            ViewData["DistrectFk"] = new SelectList(_context.TblDistricts, "Id", "DistrictName", tblUser.DistrectFk);
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName", tblUser.GovernateFk);
            ViewData["UserTypeFk"] = new SelectList(_context.TblUserTypes, "Id", "UserTypeName", tblUser.UserTypeFk);
            return View(tblUser);
        }

        // GET: TblUsers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            ViewBag.CurrentUserType = currentUserType;

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات";
                return RedirectToAction("Index", "Home"); // أو RedirectToAction لصفحة مخصصة
            }

            if (id == null)
            {
                return NotFound();
            }

            var userToEdit = await _context.TblUsers.FindAsync(id);
            if (userToEdit == null)
            {
                return NotFound();
            }

            // Check permission: Admin can edit anyone. Agent (type 6) can edit users within their company.
            // Rep (type 7) can only edit their direct traders.
            if (currentUserType == 6 && userToEdit.CompanyFk != currentUserCompanyId)
            {
                return Forbid("ليس لديك صلاحية لتعديل مستخدمين خارج شركتك.");
            }
            if (currentUserType == 7 && userToEdit.ParentUser != currentUserId)
            {
                return Forbid("ليس لديك صلاحية لتعديل مستخدمين لا يتبعون لك.");
            }

            // 1. جلب المستخدم من قاعدة البيانات مع الحساب المرتبط به
            var account = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == id);

            // 2. **هنا الحل**: تحويل البيانات من TblUser إلى AddUserViewModel
            var model = new AddUserViewModel
            {
                // إضافة خاصية Id إلى ViewModel لتمريرها في النموذج
                Id = userToEdit.Id, // <-- ستحتاج لإضافة هذه الخاصية إلى ViewModel
                FullName = userToEdit.FullName,
                ContactNo = userToEdit.ContactNo,
                Address = userToEdit.Address,
                Street = userToEdit.Street,
                // استخدام GetValueOrDefault() لتحويل int? إلى int بأمان
                Governate_FK = userToEdit.GovernateFk ?? 1,
                Distrect_FK = userToEdit.DistrectFk ?? 1,
                UserTypeFk = userToEdit.UserTypeFk <= 8 ? userToEdit.UserTypeFk : 8,
                UserName = userToEdit.UserName,
                Email = userToEdit.Email, // تأكد من جلب Email
                Naid = userToEdit.Naid, // تأكد من جلب Naid
                Indebtedness = account?.Indebtedness, // جلب المديونية من جدول الحسابات
                AllwdCrdtLmt = account?.AllwdCrdtLmt ?? 0, // تأكد من جلب AllwdCrdtLmt
                CompanyFk = userToEdit.CompanyFk, // <-- جلب CompanyFk
                BranchFk = userToEdit.BranchFk, // <-- جلب BranchFk
                Code = userToEdit.UserCode,
                IsActive = userToEdit.IsActive,
                SubEndDate = userToEdit.SubEndDate ?? DateOnly.FromDateTime(DateTime.Now),
                Representative = userToEdit.ParentUser ?? 1,
                ApprvDate = (DateOnly)userToEdit.StartDate
                // لا نمرر كلمة المرور إلى الواجهة أبداً
            };

            // 3. ملء القوائم المنسدلة
            PopulateDropdownsForEdit(model, currentUserType); // استخدام دالة مساعدة جديدة لتجنب الأخطاء

            return View(model); // 4. إرسال الـ ViewModel الصحيح إلى الواجهة
        }

        // POST: TblUsers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AddUserViewModel model)
        {
            // Security check: ensure the user has permission
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            ViewBag.CurrentUserType = currentUserType;

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            // التحقق المبدئي من تطابق الـ ID
            if (id != model.Id)
            {
                return NotFound();
            }

            // Security check: If trying to edit a user they don't have permission for
            // ... (التحقق من الـ ID والمستخدم الأصلي) ...
            var userToEditOriginal = await _context.TblUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (userToEditOriginal == null) return NotFound();

            // Security checks (re-evaluate on POST to prevent tampering)
            // ... (التحقق من صلاحية الوصول للشركة والمندوب) ...
            if (currentUserType == 6 && userToEditOriginal.CompanyFk != currentUserCompanyId)
            {
                return Forbid("ليس لديك صلاحية لتعديل مستخدمين خارج شركتك.");
            }

            if (currentUserType == 7 && userToEditOriginal.ParentUser != currentUserId)
            {
                return Forbid("ليس لديك صلاحية لتعديل مستخدمين لا يتبعون لك.");
            }

            // Prevent changing UserType if current user is not Admin (type < 6)
            if (currentUserType > 6 && model.UserTypeFk != userToEditOriginal.UserTypeFk)
            {
                ModelState.AddModelError("UserTypeFk", "ليس لديك صلاحية لتغيير صلاحية المستخدم.");
            }

            // Remove validation for fields that are optional or conditionally hidden/set
            //ModelState.Remove("SubEndDate");     // Handled by logic or not direct input for edit
            // إزالة التحقق من صحة الحقول الاختيارية في سيناريو التعديل
            // هذا يمنع ظهور أخطاء غير مرغوب فيها عند ترك الحقول فارغة

            //إذا كان المستخدم الحالي وكيل أو مندوب (>= 6)
            if (currentUserType >= 6)
            {
                ModelState.Remove("CompanyFk");
                ModelState.Remove("BranchFk");
                ModelState.Remove("SubEndDate");
                model.CompanyFk = userToEditOriginal.CompanyFk; // Retain original value
                model.BranchFk = userToEditOriginal.BranchFk;   // Retain original value
                model.SubEndDate = userToEditOriginal.SubEndDate ?? DateOnly.FromDateTime(DateTime.Now); // استعادة القيمة الأصلية من قاعدة البيانات
            }

            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");
            //ModelState.Remove("Representative"); // ليس حقل إدخال في نموذج التعديل
            //ModelState.Remove("Code"); // ليس حقل إدخال في نموذج التعديل
            ModelState.Remove("ApprvDate");

            if (string.IsNullOrEmpty(model.UserName))
            {
                ModelState.Remove("UserName");
            }

            // إذا كان المستخدم الحالي لا يملك صلاحية تعديل المديونية، يمكننا إزالتها من التحقق
            if (currentUserType >= 7)
            {
                ModelState.Remove("Indebtedness");
                ModelState.Remove("AllwdCrdtLmt");
                // الحفاظ على القيم الأصلية من قاعدة البيانات
                model.Indebtedness = _context.TblAccounts.AsNoTracking().FirstOrDefault(a => a.UserIdFk == id)?.Indebtedness ?? 0;
                model.AllwdCrdtLmt = _context.TblAccounts.AsNoTracking().FirstOrDefault(a => a.UserIdFk == id)?.AllwdCrdtLmt ?? 0;
            }

            // Username uniqueness check (if provided)
            if (!string.IsNullOrEmpty(model.UserName) && _context.TblUsers.Any(u => u.UserName == model.UserName && u.Id != id))
            {
                ModelState.AddModelError("UserName", "اسم المستخدم هذا موجود بالفعل.");
            }

            // Username is allowed to be empty on POST if user doesn't want to change it.
            //if (string.IsNullOrEmpty(model.UserName))
            //{
            //    ModelState.Remove("UserName");
            //}
            //else if (_context.TblUsers.Any(u => u.UserName == model.UserName && u.Id != id))
            //{
            //    // If username is provided, check for uniqueness (excluding current user)
            //    ModelState.AddModelError("UserName", "اسم المستخدم هذا موجود بالفعل.");
            //}

            if (ModelState.IsValid)
            {
                // ملاحظة هامة: يجب التأكد أن دالة _userService.UpdateUserAndRelatedEntities
                // تقوم بتحديث حقل ParentUser بناءً على قيمة model.Representative
                // إذا لم تكن تفعل ذلك، يجب تحديثه يدوياً هنا قبل استدعاء الخدمة أو تعديل الخدمة.

                // تحديث يدوي سريع لضمان انتقال التاجر (يمكنك نقله للـ Service لاحقاً)
                if (currentUserType <= 6) // إذا كان المعدل هو الوكيل
                {
                    var userInDb = await _context.TblUsers.FindAsync(id);
                    if (userInDb != null)
                    {
                        userInDb.ParentUser = model.Representative; // تحديث الأب
                        userInDb.UserTypeFk = model.UserTypeFk <= 8 ? model.UserTypeFk : 8;     // تحديث الصلاحية
                                                                    // حفظ التغييرات الأساسية هنا أو ترك الخدمة تقوم بذلك
                        await _context.SaveChangesAsync();
                    }
                }

                // Call the service to perform the complex update logic
                // استدعاء الخدمة لتنفيذ منطق التحديث المعقد
                bool success = _userService.UpdateUserAndRelatedEntities(model, id);
                if (success)
                {
                    TempData["Success"] = "تم تحديث بيانات المستخدم بنجاح!";
                    return RedirectToAction(nameof(Index)); // إعادة التوجيه إلى قائمة المستخدمين بعد النجاح
                }
                else
                {
                    // إضافة خطأ عام في حال فشلت عملية الحفظ في الخدمة
                    ModelState.AddModelError("", "حدث خطأ غير متوقع أثناء حفظ البيانات. يرجى المحاولة مرة أخرى.");
                }
                // إذا فشل التحقق (ModelState.IsValid == false) أو فشلت عملية الحفظ
                // يجب إعادة ملء القوائم المنسدلة وإعادة عرض النموذج مع رسائل الخطأ
            }
            // في حالة الفشل (either ModelState.IsValid is false OR _userService.UpdateUserAndRelatedEntities failed)
            PopulateDropdownsForEdit(model, currentUserType); // أعد ملء القوائم قبل إعادة عرض الـ View
            return View(model);
        }

        // Helper method for dropdowns in AddUser
        private void PopulateDropdownsForAdd(AddUserViewModel model, int currentUserType)
        {
            model.Governates = new SelectList(_context.TblGovernates.Where(g => g.IsActive), "Id", "GovernateName", model.Governate_FK);
            model.Districts = new SelectList(_context.TblDistricts.Where(d => d.IsActive && d.GovernateFk == model.Governate_FK), "Id", "DistrictName", model.Distrect_FK);
            // 1. ابدأ بالاستعلام الأساسي (جلب كل الصلاحيات النشطة)
            var userTypesQuery = _context.TblUserTypes.Where(ut => ut.IsActive == true);

            // 2. طبق الفلترة بناءً على صلاحية المستخدم الحالي
            // لنفترض أن صلاحية "الوكيل" هي 6 وصلاحية "المندوب" هي 7 والتاجر 8
            if (currentUserType == 6) // إذا كان المستخدم الحالي "وكيل"
            {
                // اجلب فقط "المندوب" و "التاجر"
                userTypesQuery = userTypesQuery.Where(ut => ut.Id == 7 || ut.Id == 8);
            }
            else if (currentUserType == 7) // إذا كان المستخدم الحالي "مندوب"
            {
                // اجلب "التاجر" فقط
                userTypesQuery = userTypesQuery.Where(ut => ut.Id == 8);
            }
            // إذا كان المستخدم الحالي له صلاحية أخرى (مثل Admin)، سيتم عرض كل الصلاحيات النشطة

            // 3. قم بإنشاء القائمة المنسدلة من الاستعلام المفلتر
            model.UserTypes = new SelectList(userTypesQuery.ToList(), nameof(TblUserType.Id), nameof(TblUserType.UserTypeName), model.UserTypeFk);
            model.Companies = new SelectList(_context.TblCompanies.Where(c => c.IsActive), "Id", "Name", model.CompanyFk);
            model.Branches = new SelectList(_context.TblBranches.Where(b => b.IsActive && b.CompanyFk == model.CompanyFk), "Id", "Name", model.BranchFk); // Filter branches by company
        }

        // دالة مساعدة جديدة مخصصة للتعديل لتجنب أخطاء model.Districts/Governates... الخ
        private void PopulateDropdownsForEdit(AddUserViewModel model, int currentUserType)
        {
            ViewData["DistrectFk"] = new SelectList(_context.TblDistricts.Where(d => d.IsActive == true), "Id", "DistrictName", model.Distrect_FK);
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates.Where(g => g.IsActive == true), "Id", "GovernateName", model.Governate_FK);
            // 1. ابدأ بالاستعلام الأساسي (جلب كل الصلاحيات النشطة)
            var userTypesQuery = _context.TblUserTypes.Where(ut => ut.IsActive == true);

            // 2. طبق الفلترة بناءً على صلاحية المستخدم الحالي
            // لنفترض أن صلاحية "الوكيل" هي 6 وصلاحية "المندوب" هي 7 والتاجر 8
            if (currentUserType == 6) // إذا كان المستخدم الحالي "وكيل"
            {
                // اجلب فقط "المندوب" و "التاجر"
                userTypesQuery = userTypesQuery.Where(ut => ut.Id == 6 || ut.Id == 7 || ut.Id == 8);
            }
            else if (currentUserType == 7) // إذا كان المستخدم الحالي "مندوب"
            {
                // اجلب "التاجر" فقط
                userTypesQuery = userTypesQuery.Where(ut => ut.Id == 8);
            }
            // إذا كان المستخدم الحالي له صلاحية أخرى (مثل Admin)، سيتم عرض كل الصلاحيات النشطة

            // 3. قم بإنشاء القائمة المنسدلة من الاستعلام المفلتر
            model.UserTypes = new SelectList(userTypesQuery.ToList(), nameof(TblUserType.Id), nameof(TblUserType.UserTypeName), model.UserTypeFk);
            ViewData["UserTypeFk"] = new SelectList(userTypesQuery.ToList(), "Id", "UserTypeName", model.UserTypeFk);
            ViewData["CompanyFk"] = new SelectList(_context.TblCompanies.Where(c => c.IsActive), "Id", "Name", model.CompanyFk);
            // Filter branches by selected CompanyFk
            ViewData["BranchFk"] = new SelectList(_context.TblBranches.Where(b => b.IsActive && b.CompanyFk == model.CompanyFk), "Id", "Name", model.BranchFk);
            // إذا كان المستخدم الحالي وكيل (6)، نعرض له: نفسه + المناديب التابعين لشركته
            if (currentUserType == 6)
            {
                var (currentUserId, _, currentUserCompanyId, _) = GetCurrentUserInfo();

                var parentsList = _context.TblUsers
                    .Where(u => u.CompanyFk == currentUserCompanyId
                             && u.IsActive == true
                             && (u.UserTypeFk == 6 || u.UserTypeFk == 7)) // الوكيل والمناديب
                    .Select(u => new { u.Id, u.FullName })
                    .ToList();

                ViewData["Representative"] = new SelectList(parentsList, "Id", "FullName", model.Representative);
            }
        }
        // GET: TblUsers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            if (id == null)
            {
                return NotFound();
            }

            var tblUser = await _context.TblUsers
                .Include(t => t.BranchFkNavigation)
                .Include(t => t.CompanyFkNavigation)
                .Include(t => t.DirctionFkNavigation)
                .Include(t => t.DistrectFkNavigation)
                .Include(t => t.GovernateFkNavigation)
                .Include(t => t.UserTypeFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblUser == null)
            {
                return NotFound();
            }

            return View(tblUser);
        }

        // POST: TblUsers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblUser = await _context.TblUsers.FindAsync(id);
            if (tblUser != null)
            {
                _context.TblUsers.Remove(tblUser);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblUserExists(int id)
        {
            return _context.TblUsers.Any(e => e.Id == id);
        }
    }
}
