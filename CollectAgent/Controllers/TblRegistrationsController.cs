using DatabaseAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CollectAgent.Controllers
{
    public class TblRegistrationsController : Controller
    {
        private readonly CollectAgentDBContext _context;
        private const int PageSize = 10; // عدد البطاقات في كل صفحة

        // تعريف أنواع المستخدمين لتسهيل القراءة
        private const int ADMIN_USER_TYPE = 1;  // افترض أن 1 وما دونه هو إدارة
        private const int AGENT_USER_TYPE = 6;  // وكيل/تاجر
        private const int REP_USER_TYPE = 7;    // مندوب

        public TblRegistrationsController(CollectAgentDBContext context)
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

        // دالة مساعدة لجلب كل المستخدمين التابعين
        // --- دالة مساعدة لجلب الـ IDs التابعة للمستخدم الحالي ---
        // هذه الدالة ضرورية للمندوب
        private async Task<List<int>> GetSubordinateUserIdsAsync(int parentUserId)
        {
            // ملاحظة: هذه الدالة تجلب فقط التابعين المباشرين كما في TblUsersController.
            // إذا أردت جلب كل المستويات (تابع التابع)، استخدم الدالة Recursive التي ناقشناها سابقاً.
            var subordinateIds = await _context.TblUsers
                .Where(u => u.ParentUser == parentUserId)
                .Select(u => u.Id)
                .ToListAsync();

            // أضف المستخدم نفسه إلى القائمة
            subordinateIds.Add(parentUserId);
            return subordinateIds;
        }


        // GET: TblRegistrations
        public async Task<IActionResult> Index()
        {
            // 1. احصل على ID المستخدم الحالي من الـ Session
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7 || !currentUserCompanyId.HasValue)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }

            IQueryable<TblRegistration> registrationsQuery = _context.TblRegistrations;

            // 2. طبق الفلاتر بناءً على نوع المستخدم
            if (currentUserType == REP_USER_TYPE) // مندوب
            {
                var allowedUserIds = await GetSubordinateUserIdsAsync(currentUserId);
                registrationsQuery = registrationsQuery.Where(r => allowedUserIds.Contains(r.UserFk));
            }
            else if (currentUserType == AGENT_USER_TYPE) // وكيل
            {
                registrationsQuery = registrationsQuery.Where(r => r.UserFkNavigation.CompanyFk == currentUserCompanyId);
            }
            // إذا كان مدير نظام ( currentUserType < 6 )، فلن يتم تطبيق أي فلتر

            // 3. أكمل الاستعلام لجلب البيانات
            var registrations = await registrationsQuery
                 .Include(t => t.ProviderFkNavigation)
                 .Include(t => t.UserFkNavigation)
                 .OrderByDescending(r => r.Id)
                 .Take(PageSize)
                 .ToListAsync();

            ViewBag.HasMorePages = (await registrationsQuery.CountAsync()) > PageSize;
            return View(registrations);
        }

        // GET: TblRegistrations/RegBalance
        public async Task<IActionResult> RegBalance()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index));
            }

            // التعديل هنا: استخدام Where بدلاً من FirstOrDefault لجلب قائمة
            var registrations = await _context.TblRegistrations
                .Include(t => t.ProviderFkNavigation)
                .Include(t => t.UserFkNavigation)
                .Where(m => m.UserFk == currentUserId) // جلب كل السجلات الخاصة بالمستخدم
                .ToListAsync(); // تحويل النتيجة إلى قائمة

            // الصفحة ستعمل الآن بشكل صحيح حتى لو كانت القائمة فارغة
            return View(registrations);
        }

        // GET: TblRegistrations/GetRegistrations?page=2
        [HttpGet]
        public async Task<IActionResult> GetRegistrations(int page = 2)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }
            IQueryable<TblRegistration> registrationsQuery = _context.TblRegistrations;

            if (currentUserType == REP_USER_TYPE)
            {
                var allowedUserIds = await GetSubordinateUserIdsAsync(currentUserId);
                registrationsQuery = registrationsQuery.Where(r => allowedUserIds.Contains(r.UserFk));
            }
            else if (currentUserType == AGENT_USER_TYPE)
            {
                registrationsQuery = registrationsQuery.Where(r => r.UserFkNavigation.CompanyFk == currentUserCompanyId);
            }

            var registrations = await registrationsQuery
                .Include(t => t.ProviderFkNavigation)
                .Include(t => t.UserFkNavigation)
                .OrderByDescending(r => r.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            //var totalCount = await registrationsQuery.CountAsync();
            //if (page * PageSize >= totalCount)
            //{
            //    return PartialView("_RegistrationCardsPartial", new List<TblRegistration>());
            //}

            return PartialView("_RegistrationCardsPartial", registrations);
        }

        // GET: TblRegistrations/SearchRegistrations?searchTerm=...
        [HttpGet]
        public async Task<IActionResult> SearchRegistrations(string searchTerm)
        {

            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return PartialView("_RegistrationCardsPartial", new List<TblRegistration>());
            }

            IQueryable<TblRegistration> registrationsQuery = _context.TblRegistrations;

            // تطبيق الفلاتر (صلاحيات المستخدم)
            if (currentUserType == REP_USER_TYPE)
            {
                var allowedUserIds = await GetSubordinateUserIdsAsync(currentUserId);
                registrationsQuery = registrationsQuery.Where(r => allowedUserIds.Contains(r.UserFk));
            }
            else if (currentUserType == AGENT_USER_TYPE)
            {
                registrationsQuery = registrationsQuery.Where(r => r.UserFkNavigation.CompanyFk == currentUserCompanyId);
            }

            var searchTermLower = searchTerm.ToLower();

            var searchResult = await registrationsQuery
                .Include(t => t.ProviderFkNavigation)
                .Include(t => t.UserFkNavigation)
                .Where(r =>
                    // 1. البحث في اسم الحساب
                    r.AccNameNo.ToLower().Contains(searchTermLower) ||
                     // 2. البحث في كود الحساب
                     r.Code.ToLower().Contains(searchTermLower) ||
                    // 3. البحث في اسم التاجر الكامل
                    r.UserFkNavigation.FullName.ToLower().Contains(searchTermLower) ||
                    // 4. (الإضافة الجديدة) البحث في اسم المحل/الكود الخاص بالتاجر
                    (r.UserFkNavigation.UserCode != null && r.UserFkNavigation.UserCode.ToLower().Contains(searchTermLower)) ||
                    // 5. البحث برقم الهاتف
                    r.UserFkNavigation.ContactNo.Contains(searchTerm)
                // ... أضف حقول بحث أخرى إذا أردت ...
                )
                .ToListAsync();

            return PartialView("_RegistrationCardsPartial", searchResult);

        }

        // GET: TblRegistrations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index));
            }

            var tblRegistration = await _context.TblRegistrations
                .Include(t => t.ProviderFkNavigation)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblRegistration == null)
            {
                TempData["ErrorMessage"] = "عفوا حدث حطأ في عرض البيانات !!!";
                return RedirectToAction(nameof(Index));
            }

            return View(tblRegistration);
        }

        // --- دالة مساعدة جديدة لملء بيانات الـ ViewBag الخاصة بصفحة الإنشاء ---
        private async Task PopulateCreateViewBagData()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, _) = GetCurrentUserInfo();

            // 1. فلترة المستخدمين حسب الصلاحية
            IQueryable<TblUser> usersQuery = _context.TblUsers.Where(u => u.IsActive == true); // جلب المستخدمين النشطين فقط

            if (currentUserType == REP_USER_TYPE) // مندوب
            {
                var allowedUserIds = await GetSubordinateUserIdsAsync(currentUserId);
                usersQuery = usersQuery.Where(u => allowedUserIds.Contains(u.Id));
            }
            else if (currentUserType == AGENT_USER_TYPE) // وكيل
            {
                usersQuery = usersQuery.Where(u => u.CompanyFk == currentUserCompanyId);
            }
            // لا يوجد فلتر للمدير (يرى الجميع)

            // 2. تجهيز القائمة المنسدلة بشكل أفضل (عرض الاسم + الرقم)
            var userList = await usersQuery
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    Id = u.Id,
                    DisplayText = u.FullName + " (" + u.ContactNo + ")" // مثال: "أحمد علي (091xxxxxxx)"
                })
                .ToListAsync();

            ViewData["UserFk"] = new SelectList(userList, "Id", "DisplayText");

            // 3. جلب مزودي الخدمة (نفترض أنهم متاحون للجميع حالياً)
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders.Where(p => p.ProvTypeFk == 2 && p.IsActive == true), "Id", "ProviderName"); // استخدم ProviderName للعرض
        }

        // GET: TblRegistrations/Create
        public async Task<IActionResult> Create()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }
            // <<< تعديل: استدعاء الدالة المساعدة لملء البيانات بالصلاحيات الصحيحة
            await PopulateCreateViewBagData();

            // إرسال موديل جديد بقيم افتراضية لتحسين تجربة المستخدم
            var model = new TblRegistration
            {
                StartDate = DateOnly.FromDateTime(DateTime.Now) // تحديد تاريخ اليوم كقيمة افتراضية
            };

            return View(model);
        }

        // POST: TblRegistrations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        // قمنا بتعديل Bind ليشمل حقل IsActive إذا أردت إضافته للنموذج
        public async Task<IActionResult> Create([Bind("UserFk,AccNameNo,Code,ProviderFk,Balance,StartDate")] TblRegistration tblRegistration)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }
            // إضافة قيمة افتراضية للحقول التي ليست في النموذج
            tblRegistration.IsActive = true;

            if (ModelState.IsValid)
            {
                // التحقق من عدم وجود حساب بنفس الرقم (AccNameNo) لنفس مزود الخدمة
                bool accountExists = await _context.TblRegistrations
                    .AnyAsync(r => r.AccNameNo == tblRegistration.AccNameNo && r.ProviderFk == tblRegistration.ProviderFk);

                if (accountExists)
                {
                    // إضافة خطأ مخصص للموديل
                    ModelState.AddModelError("AccNameNo", "هذا الحساب مسجل بالفعل مع نفس مزود الخدمة.");
                }
                else
                {
                    _context.Add(tblRegistration);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم إنشاء الحساب بنجاح."; // رسالة نجاح للمستخدم
                    return RedirectToAction(nameof(Index));
                }
            }

            // <<< تعديل: إذا فشل الحفظ، أعد ملء القوائم المنسدلة بالبيانات المفلترة
            await PopulateCreateViewBagData();
            return View(tblRegistration);
        }

        // GET: TblRegistrations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }

            if (id == null)
            {
                TempData["ErrorMessage"] = "عفوا حدث خطأ في عرض البيانات !!!";
                return RedirectToAction(nameof(Index));
            }

            // --- التحقق من الصلاحية (مهم جداً) ---
            //var (currentUserId, currentUserType, currentUserCompanyId, _) = GetCurrentUserInfo();

            var tblRegistration = await _context.TblRegistrations
                                                .Include(r => r.UserFkNavigation) // <-تحميل بيانات صاحب الحساب - تحميل البيانات المرتبطة
                                                .FirstOrDefaultAsync(r => r.Id == id);

            if (tblRegistration == null)
            {
                TempData["ErrorMessage"] = "خطأ عرض البيانات !!";
                return RedirectToAction(nameof(Index));
            }

            // 3. تطبيق منطق الصلاحيات الهرمي
            bool isAuthorized = false;
            int targetUserType = tblRegistration.UserFkNavigation.UserTypeFk; // رتبة صاحب الحساب

            // الحالة الأولى: الإدارة (من 1 إلى 5)
            if (currentUserType < 6)
            {
                // الإدارة يمكنها تعديل أي شخص رتبته "أكبر" (أقل صلاحية) منها
                // مثال: رقم 2 يمكنه تعديل 3، 4، 5، 6... لكن لا يمكنه تعديل 1 أو 2
                if (targetUserType >= currentUserType)
                {
                    isAuthorized = true;
                }
            }
            else if (currentUserType == AGENT_USER_TYPE) // الحالة الثانية: الوكيل (6)
            {
                // الوكيل يعدل فقط من هم في شركته ورتبتهم أكبر منه (مثل المندوب 7 والتاجر 8)
                if (tblRegistration.UserFkNavigation.CompanyFk == currentUserCompanyId && targetUserType > currentUserType)
                {
                    isAuthorized = true;
                }
                // الشرط الثاني: هل يعدل حسابه الشخصي؟ (مقارنة UserID مع UserFk)
                // هذا الشرط يضمن أن الوكيل يعدل نفسه، لكن لا يعدل وكيل آخر (لأن ID سيختلف)
                else if (tblRegistration.UserFk == currentUserId)
                {
                    isAuthorized = true;
                }
            }
            else if (currentUserType == REP_USER_TYPE) // الحالة الثالثة: المندوب (7)
            {
                // المندوب يعدل التابعين له فقط
                var allowedUserIds = await GetSubordinateUserIdsAsync(currentUserId);
                if (allowedUserIds.Contains(tblRegistration.UserFk))
                {
                    isAuthorized = true;
                }
            }

            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }

            // --- استدعاء الدالة المساعدة لملء ViewBag ---
            await PopulateViewBagData(tblRegistration.UserFk, tblRegistration.ProviderFk);

            // ✅✅✅ أضف هذا السطر المهم جداً ✅✅✅
            // نرسل نوع المستخدم الحالي (الذي فتح الصفحة) لنتحكم في ظهور حقل الرصيد
            ViewBag.CurrentUserType = currentUserType;

            // --- استدعاء الدالة المساعدة لملء ViewBag ---
            await PopulateViewBagData(tblRegistration.UserFk, tblRegistration.ProviderFk);

            ViewBag.CurrentUserType = currentUserType;

            // ✅✅✅ الإضافة الجديدة: تجهيز قائمة التجار لنقل الماكينة (للإدارة والوكيل فقط) ✅✅✅
            if (currentUserType <= AGENT_USER_TYPE)
            {
                IQueryable<TblUser> usersQuery = _context.TblUsers.Where(u => u.IsActive == true);

                // إذا كان وكيل، يرى تجار شركته فقط. أما الإدارة فترى الجميع
                if (currentUserType == AGENT_USER_TYPE)
                {
                    usersQuery = usersQuery.Where(u => u.CompanyFk == currentUserCompanyId);
                }

                // 1. جلب البيانات الخام من قاعدة البيانات أولاً (بدون دمج)
                var rawUsers = await usersQuery.Select(u => new {
                    u.Id,
                    u.FullName,
                    u.UserCode,
                    u.ContactNo
                }).ToListAsync();

                // 2. دمج النصوص داخل C# (وهذا يمنع خطأ الـ Collation تماماً)
                var userList = rawUsers.Select(u => new {
                    Id = u.Id,
                    Name = u.FullName + " (" + (string.IsNullOrWhiteSpace(u.UserCode) ? u.ContactNo : u.UserCode) + ")"
                }).ToList();

                ViewBag.UserFkList = new SelectList(userList, "Id", "Name", tblRegistration.UserFk);
            }

            return View(tblRegistration);
        }

        // POST: TblRegistrations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        // POST: TblRegistrations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        // قمنا بتعديل الـ Bind ليطابق الحقول الموجودة في النموذج فقط
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserFk,ProviderFk,AccNameNo,Code,IsActive,Balance,StartDate")] TblRegistration formData)
        {
            if (id != formData.Id)
            {
                TempData["ErrorMessage"] = "خطأ عرض البيانات";
                return RedirectToAction(nameof(Index));
            }

            // --- 🛑 الخطوة 1: جلب الكائن الكامل من قاعدة البيانات مع الكائنات المرتبطة ---
            // هذا يضمن أن 'UserFkNavigation' لن يكون null أبداً
            var registrationToUpdate = await _context.TblRegistrations
                                                     .Include(r => r.UserFkNavigation)
                                                     .FirstOrDefaultAsync(r => r.Id == id);

            if (registrationToUpdate == null)
            {
                TempData["ErrorMessage"] = "خطأ عرض البيانات";
                return RedirectToAction(nameof(Index));
            }

            // --- 🛑 الخطوة 2: التحقق من الصلاحية (مهم جداً) ---
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }
            bool isAuthorized = false;
            int targetUserType = registrationToUpdate.UserFkNavigation.UserTypeFk;

            if (currentUserType < 6)
            {
                // استخدام >= للسماح للادمن بتعديل نفسه
                if (targetUserType >= currentUserType) isAuthorized = true;
            }
            else if (currentUserType == AGENT_USER_TYPE)
            {
                // 1. تعديل التابعين في نفس الشركة
                if (registrationToUpdate.UserFkNavigation.CompanyFk == currentUserCompanyId && targetUserType > currentUserType)
                {
                    isAuthorized = true;
                }
                //// 2. تعديل النفس (تمت إضافتها هنا لأنها كانت ناقصة) ✅
                else if (registrationToUpdate.UserFk == currentUserId)
                {
                    isAuthorized = true;
                }
            }
            else if (currentUserType == REP_USER_TYPE)
            {
                var allowedUserIds = await GetSubordinateUserIdsAsync(currentUserId);
                if (allowedUserIds.Contains(registrationToUpdate.UserFk))
                    isAuthorized = true;
            }

            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "عفواً، غير مصرح لك بتعديل هذا الحساب !!!";
                return RedirectToAction("Index", "Home"); // منع الوصول إذا لم يكن مصرحاً له
            }

            // --- 🛑 الخطوة 3: التحقق من صحة النموذج المرسل ---
            if (ModelState.IsValid)
            {
                // إذا كان النموذج صالحاً، نقوم بتحديث الحقول المسموح بها من النموذج
                registrationToUpdate.AccNameNo = formData.AccNameNo;
                registrationToUpdate.Code = formData.Code;
                // registrationToUpdate.StartDate = formData.StartDate; // أنت أخفيته من النموذج
                registrationToUpdate.IsActive = formData.IsActive;
                // مثال: السماح فقط للادمن بتعديل الرصيد ونقل الملكية لتاجر آخر
                if (currentUserType <= AGENT_USER_TYPE)
                {
                    // ✅✅✅ أضف هذا السطر لحفظ الرصيد الجديد ✅✅✅
                    registrationToUpdate.Balance = formData.Balance;

                    // ✅✅✅ سطر واحد لنقل الماكينة (تحديث التاجر) ✅✅✅
                    registrationToUpdate.UserFk = formData.UserFk;
                }
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث بيانات الحساب بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblRegistrationExists(formData.Id)) { return NotFound(); } else { throw; }
                }
            }

            // --- 🛑 !! الجزء الحاسم لحل المشكلة !! ---
            // إذا فشل التحقق (ModelState is invalid)
            // لا نرسل formData غير المكتمل، بل نرسل الكائن registrationToUpdate الكامل الذي جلبناه
            // هذا يضمن أن UserFkNavigation موجود دائماً في النموذج
            await PopulateViewBagData(registrationToUpdate.UserFk, registrationToUpdate.ProviderFk);

            // نقوم بتحديث الكائن الذي سنعيده بالقيم التي أدخلها المستخدم ليعرضها مرة أخرى مع رسائل الخطأ
            registrationToUpdate.AccNameNo = formData.AccNameNo;
            registrationToUpdate.Code = formData.Code;
            registrationToUpdate.IsActive = formData.IsActive;
            registrationToUpdate.Balance = formData.Balance; // تحديث الرصيد في العرض أيضاً عند الخطأ

            ViewBag.CurrentUserType = currentUserType;

            // ✅ إعادة ملء قائمة التجار في حالة الخطأ لكي لا تفرغ القائمة
            if (currentUserType <= AGENT_USER_TYPE)
            {
                IQueryable<TblUser> usersQuery = _context.TblUsers.Where(u => u.IsActive == true);

                if (currentUserType == AGENT_USER_TYPE)
                {
                    usersQuery = usersQuery.Where(u => u.CompanyFk == currentUserCompanyId);
                }

                // جلب البيانات الخام
                var rawUsers = await usersQuery.Select(u => new { u.Id, u.FullName, u.UserCode, u.ContactNo }).ToListAsync();

                // الدمج في C#
                var userList = rawUsers.Select(u => new {
                    Id = u.Id,
                    Name = u.FullName + " (" + (string.IsNullOrWhiteSpace(u.UserCode) ? u.ContactNo : u.UserCode) + ")"
                }).ToList();

                ViewBag.UserFkList = new SelectList(userList, "Id", "Name", formData.UserFk);
            }

            return View(registrationToUpdate); // <-- نرسل الكائن الكامل والمحدث
        }

        // --- دالة مساعدة لملء بيانات ViewBag للقراءة فقط ---
        private async Task PopulateViewBagData(int userFk, int providerFk)
        {
            // نفترض أن اسم المستخدم موجود في حقل FullName
            var user = await _context.TblUsers.FindAsync(userFk);
            ViewBag.Usercode = user?.UserCode ?? "غير معروف";

            // نفترض أن اسم مزود الخدمة في حقل ProviderName
            var provider = await _context.TblProviders.FindAsync(providerFk);
            ViewBag.ProviderName = provider?.ProviderName ?? "غير معروف";
        }

        // GET: TblRegistrations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }

            if (id == null)
            {
                return NotFound();
            }

            var tblRegistration = await _context.TblRegistrations
                .Include(t => t.ProviderFkNavigation)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblRegistration == null)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "عفوا حدث خطأ في عرض البيانات !!!";
                return RedirectToAction(nameof(Index)); // أو RedirectToAction لصفحة مخصصة
            }

            return View(tblRegistration);
        }

        // POST: TblRegistrations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblRegistration = await _context.TblRegistrations.FindAsync(id);
            if (tblRegistration != null)
            {
                _context.TblRegistrations.Remove(tblRegistration);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblRegistrationExists(int id)
        {
            return _context.TblRegistrations.Any(e => e.Id == id);
        }
    }
}
