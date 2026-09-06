using CollectAgent.ViewModels;
using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting; // <-- CORRECTION: Added for file paths
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Scaffolding.Shared;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO; // <-- CORRECTION: Added for file operations
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CollectAgent.Controllers
{
    [Authorize] // <<< هذا السطر يحمي الـ Controller بالكامل
    public class TblDepositOrdersController : Controller
    {
        private readonly CollectAgentDBContext _context;
        private const int PageSize = 10; // عدّل الرقم كما تريد

        private readonly IWebHostEnvironment _webHostEnvironment; // <-- CORRECTION: Injected for file uploads

        public TblDepositOrdersController(CollectAgentDBContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment; // <-- CORRECTION: Initialize the environment variable
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

        // GET: TblDepositOrders
        public async Task<IActionResult> Index(string searchTerm, DateOnly? fromDate, DateOnly? toDate)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.currentUserType = currentUserType;
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
            // <-- CORRECTION: Simplified logic and corrected query execution
            //IQueryable<TblDepositOrder> query = _context.TblDepositOrders
            //    .Include(t => t.DepositForNavigation)
            //    .Include(t => t.ProviderIdFkNavigation)
            //    .Include(t => t.Status)
            //    .Include(t => t.UserIdFkNavigation);

            ////List<TblDepositOrder> tblDepositOrders = new List<TblDepositOrder>();

            //if (currentUserType < 8) // Admin/Super-user view
            //{
            //    query = query.OrderByDescending(t => t.ReferenceCode);
            //}
            //else // Regular user view
            //{
            //    query = query.Where(d => d.UserIdFk == currentUserId).OrderByDescending(t => t.Date);
            //}
            //var tblDepositOrders = await query.ToListAsync();
            //return View(tblDepositOrders);
        }

        // ✅ 2. إضافة Action جديدة لجلب البيانات عند التمرير
        [HttpGet]
        public async Task<IActionResult> LoadMoreDeposits(string searchTerm, DateOnly? fromDate, DateOnly? toDate, int page = 2)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
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
        private IQueryable<TblDepositOrder> GetFilteredDepositsQuery(string searchTerm, DateOnly? fromDate, DateOnly? toDate)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            var query = _context.TblDepositOrders
                .Include(t => t.ProviderIdFkNavigation)
                .Include(t => t.Status)
                .Include(t => t.UserIdFkNavigation) // نحتاجها لجلب CompanyFk الخاص بصاحب الإيداع
                .Include(t => t.DepositForNavigation) // <==== (أضف هذا السطر هنا) جلب بيانات المستفيد
                .AsQueryable();

            // --- 1. الإداريون (UserType < 6): يرون كل شيء ---
            if (currentUserType < 6)
            {
                // لا نضيف أي شرط تصفية، يرى جميع الإيداعات
            }
            // --- 2. الوكلاء (UserType = 6): يرون إيداعات كل المستخدمين في نفس الشركة ---
            else if (currentUserType == 6 && currentUserCompanyId.HasValue)
            {
                // جلب جميع معرفات المستخدمين في نفس الشركة (المندوبين + الوكيل نفسه)
                var companyUserIds = _context.TblUsers
                    .Where(u => u.CompanyFk == currentUserCompanyId.Value && u.IsActive == true)
                    .Select(u => u.Id)
                    .ToList();

                // تصفية الإيداعات: فقط للمستخدمين الذين ينتمون لنفس الشركة
                query = query.Where(t => companyUserIds.Contains(t.UserIdFk));
            }
            // --- 3. المندوبون (UserType = 7)  يرون إيداعاتهم الشخصية التامة فقط ---
            else if (currentUserType == 7)
            {

                query = query.Where(t => t.UserIdFk == currentUserId && t.StatusId == 2);
            }
            // --- 4. أي نوع مستخدم آخر غير معروف: منع الوصول (احتياط إضافي) ---
            else
            {
                // إرجاع query فارغ (لن يرى أي شيء)
                query = query.Where(t => false);
            }

            // ترتيب النتائج
            query = query.OrderByDescending(t => t.Date)
                         .ThenByDescending(t => t.Id);

            // تطبيق فلاتر البحث إن وجدت
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(t => t.ReferenceCode.Contains(searchTerm));
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
        // GET: TblDepositOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            //int senderid = (int)Session["UserID"];

            if (id == null)
            {
                return BadRequest();
            }
            // <-- CORRECTION: Cleaned up redundant code. One clear query.
            var tblDepositOrder = await _context.TblDepositOrders
                .Include(t => t.DepositForNavigation)
                .Include(t => t.ProviderIdFkNavigation)
                .Include(t => t.Status)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tblDepositOrder == null)
            {
                // <-- CORRECTION: Use modern ASP.NET Core return types
                TempData["ErrorMessage"] = " بيانات غير موجودة !!!";
                return RedirectToAction("Index", "Home");
            }

            return View(tblDepositOrder);
        }

        // GET: TblDepositOrders/DepositType
        public IActionResult DepositType()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }
            // ✅ الإضافة: نقوم بتمرير نوع المستخدم إلى الـ View
            ViewBag.UserType = currentUserType;
            return View();
        }

        // GET: TblDepositOrders/Create
        public IActionResult Create(int type, int? merchantId = null)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            // التحقق من وجود إيداع معلق
            var oldOrder = _context.TblDepositOrders.FirstOrDefault(d => d.UserIdFk == currentUserId && d.StatusId == 1);
            if (oldOrder != null)
            {
                //TempData["Success"] = "!!! عفوا يوجد ايداع معلق ، انتظر الرد ";
                TempData["ErrorMessage"] = "!!! عفوا يوجد ايداع معلق ، انتظر الرد ";
                return RedirectToAction("Index", "Home");
            }

            // التحقق من النقدية
            var moneyOwned = _context.TblDrawers.FirstOrDefault(d => d.UserFk == currentUserId);

            if (moneyOwned == null || moneyOwned.TtlAmntOwned <= 0)
            {
                //TempData["Success"] = "!!! لا يوجد نقدية لعـمل ايـداع ";
                TempData["ErrorMessage"] = "!!! لا يوجد نقدية لعمل ايداع ";
                return RedirectToAction("Index", "Home");
            }

            DepositTransAction deposit = new DepositTransAction
            {
                UserID = currentUserId,
                DepositType = type,
                DepositTo = merchantId, // ✅ التعديل: تعيين المستفيد في الموديل إذا تم إرساله
                Date = DateOnly.FromDateTime(DateTime.Today), // <-- التصحيح
                Own200 = (int?)moneyOwned.Le200 ?? 0,
                Own100 = (int?)moneyOwned.Le100 ?? 0,
                Own50 = (int?)moneyOwned.Le50 ?? 0,
                Own20 = (int?)moneyOwned.Le20 ?? 0,
                Own10 = (int?)moneyOwned.Le10 ?? 0,
                Own5 = (int?)moneyOwned.Le5 ?? 0,
                TtlAmntOwned = moneyOwned.TtlAmntOwned ?? 0
            };

            ViewBag.ProviderID = new SelectList(_context.TblProviders.Where(p => p.IsActive == true), "Id", "ProviderName");
            
            var usersQuery = _context.TblUsers.Where(u => u.IsActive == true && u.UserCode != null && u.CompanyFk == currentUserCompanyId).ToList();
            // ✅ التعديل: تمرير merchantId كقيمة مختارة (SelectedValue) في الـ SelectList
            // المعامل الرابع في SelectList هو القيمة المختارة افتراضياً
            ViewBag.DepositTo = new SelectList(usersQuery, "Id", "UserCode", merchantId);

            TblDrawer userDrawer = _context.TblDrawers.FirstOrDefault(d => d.UserFk == currentUserId);
            ViewBag.userDrawer = userDrawer.Id;
            return View(deposit);
        }

        // POST: TblDepositOrders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]        // <-- CORRECTION: Simplified the parameters. Use one ViewModel to receive all data including the file.
        public async Task<IActionResult> Create(DepositTransAction model)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            // الخطوة الأولى: التحقق من صحة البيانات
            if (!ModelState.IsValid)
            {
                // إذا كانت البيانات غير صحيحة، أعد تحميل القوائم المنسدلة وأرجع المستخدم لنفس الصفحة لعرض الأخطاء
                ViewBag.ProviderID = new SelectList(_context.TblProviders.Where(p => p.IsActive == true), "Id", "ProviderName", model.ProviderID);
                ViewBag.DepositTo = new SelectList(_context.TblUsers.Where(u => u.IsActive == true), "Id", "ShopName", model.DepositTo);
                return View(model);
            }

            // الخطوة الثانية: التعامل مع رفع الصورة (بالطريقة الصحيحة)
            string uniqueFileName = null;
            if (model.DepositPic != null)
            {
                // تحديد مسار مجلد wwwroot/Content/DepositPic
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "Content/DepositPic");

                // التأكد من وجود المجلد، وإن لم يكن موجوداً يتم إنشاؤه
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // إنشاء اسم فريد للصورة لمنع تكرار الأسماء
                uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.DepositPic.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // حفظ الصورة في المسار المحدد
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.DepositPic.CopyToAsync(fileStream);
                }
            }

            model.ReferenceCode = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Egypt Standard Time").ToString("yyMMddhhmmssff");
            int? Le500 = (model.LE500 > 0) ? model.LE500 : 0;
            int? Le200 = (model.LE200 > 0) ? model.LE200 : 0;
            int? Le100 = (model.LE100 > 0) ? model.LE100 : 0;
            int? Le50 = (model.LE50 > 0) ? model.LE50 : 0;
            int? Le20 = (model.LE20 > 0) ? model.LE20 : 0;
            int? Le10 = (model.LE10 > 0) ? model.LE10 : 0;
            int? Le5 = (model.LE5 > 0) ? model.LE5 : 0;

            int depositType = model.DepositType;

            // الخطوة الثالثة: إنشاء كائن قاعدة البيانات وحفظه
            TblDepositOrder depositOrder = new TblDepositOrder()
            {
                ReferenceCode = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Egypt Standard Time").ToString("yyMMddHHmmssff"),
                ProviderIdFk = (model.DepositType == 1 || model.DepositType == 2) ? model.ProviderID : 1,
                DepositFor = (model.DepositType == 2 || model.DepositType == 3) ? model.DepositTo : 1,
                DepositAccNo = (model.DepositType == 1 || model.DepositType == 2) ? model.DepositAccNo : "0",
                Date = DateOnly.FromDateTime(model.DateTimeLocal),                // حفظ المسار النسبي للصورة في قاعدة البيانات
                DepositPhoto = uniqueFileName != null ? $"/Content/DepositPic/{uniqueFileName}" : null,
                DepositAmount = model.DepositAmount,
                Le500 = model.LE500 ?? 0,
                Le200 = model.LE200 ?? 0,
                Le100 = model.LE100 ?? 0,
                Le50 = model.LE50 ?? 0,
                Le20 = model.LE20 ?? 0,
                Le10 = model.LE10 ?? 0,
                Le5 = model.LE5 ?? 0,
                DepositType = model.DepositType,
                UserIdFk = currentUserId,
                StatusId = 1 // Pending
            };

            _context.TblDepositOrders.Add(depositOrder);
            await _context.SaveChangesAsync();
            // 1. توليد الرابط لصفحة التوجيه (Index الخاصة بـ TblDepositOrders)
            string depositUrl = Url.Action("Index", "TblDepositOrders");

            // الخطوة الرابعة: إرسال رسالة نجاح وإعادة التوجيه
            TempData["Success"] = $"تم رفع طلب الايداع بنجاح، انتظر الرد. <a href='{depositUrl}' class='alert-link fw-bold ms-2' style='text-decoration: underline;'>👉 اضغط هنا للذهاب لسجل الإيداعات</a>";
            return RedirectToAction("Index", "Home");
        }

        // GET: TblDepositOrders/Edit/5
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
                return NotFound(); // الطريقة الصحيحة في ASP.NET Core
            }

            var tblDepositOrder = await _context.TblDepositOrders.FindAsync(id);

            if (tblDepositOrder == null)
            {
                return NotFound();
            }

            // إعداد القوائم المنسدلة للـ View
            ViewData["DepositFor"] = new SelectList(_context.TblUsers, "Id", "ShopName", tblDepositOrder.DepositFor);
            ViewData["ProviderIdFk"] = new SelectList(_context.TblProviders, "Id", "ProviderName", tblDepositOrder.ProviderIdFk);
            ViewData["StatusId"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblDepositOrder.StatusId);

            return View(tblDepositOrder);
        }

        // POST: TblDepositOrders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserIdFk,ReferenceCode,ProviderIdFk,DepositAccNo,Date,DepositPhoto,DepositAmount,Le500,Le200,Le100,Le50,Le20,Le10,Le5,DepositFor,DepositType,StatusId")] TblDepositOrder tblDepositOrder)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (id != tblDepositOrder.Id)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblDepositOrder);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblDepositOrderExists(tblDepositOrder.Id))
                    {
                        TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["DepositFor"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDepositOrder.DepositFor);
            ViewData["ProviderIdFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblDepositOrder.ProviderIdFk);
            ViewData["StatusId"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblDepositOrder.StatusId);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblDepositOrder.UserIdFk);
            return View(tblDepositOrder);
        }

        [HttpPost] // <-- هام جداً: يجب أن تكون POST
        [ValidateAntiForgeryToken] // <-- لحماية الطلب
        public async Task<IActionResult> DepositConfirm(int id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    TblDepositOrder tblDepositOrder = await _context.TblDepositOrders.Where(o => o.UserIdFk == currentUserId && o.StatusId == 1).FirstOrDefaultAsync();

                    if (tblDepositOrder == null || tblDepositOrder.StatusId != 1) // تأكد أن الطلب ما زال معلقاً
                    {
                        // إذا لم يتم العثور على الطلب أو تم تأكيده بالفعل، تراجع وأظهر خطأ
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = "هذا الطلب غير موجود أو تم التعامل معه بالفعل.";
                        return RedirectToAction("Index");
                    }

                    var deposer = tblDepositOrder.UserIdFk;
                    tblDepositOrder.StatusId = 2;
                    var receiverId = (tblDepositOrder.ProviderIdFk > 1) ? tblDepositOrder.ProviderIdFk : tblDepositOrder.DepositFor;
                    var referenceCode = DateTime.Now.ToString("yyMMddhhmmssff");
                    var transDate = DateOnly.FromDateTime(DateTime.Today); // <-- التصحيح
                    var dateAndTime = Convert.ToDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Egypt Standard Time").ToString(format: "F"));
                    var depositAmount = tblDepositOrder.DepositAmount;
                    var depositType = tblDepositOrder.DepositType;
                    var depositFor = tblDepositOrder.DepositFor;
                   
                    int le200 = tblDepositOrder.Le200 ?? 0;
                    int le100 = tblDepositOrder.Le100 ?? 0;
                    int le50 = tblDepositOrder.Le50 ?? 0;
                    int le20 = tblDepositOrder.Le20 ?? 0;
                    int le10 = tblDepositOrder.Le10 ?? 0;
                    int le5 = tblDepositOrder.Le5 ?? 0;

                    TblDrawer senderDrawer = await _context.TblDrawers.FirstOrDefaultAsync(d => d.UserFk == deposer);
                    int befr200 = (int)senderDrawer.Le200;
                    int befr100 = (int)senderDrawer.Le100;
                    int befr50 = (int)senderDrawer.Le50;
                    int befr20 = (int)senderDrawer.Le20;
                    int befr10 = (int)senderDrawer.Le10;
                    int befr5 = (int)senderDrawer.Le5;
                    int befrTotalAmnt = (int)senderDrawer.TtlAmntOwned;
                    int aftr200 = befr200 - le200;
                    int aftr100 = befr100 - le100;
                    int aftr50 = befr50 - le50;
                    int aftr20 = befr20 - le20;
                    int aftr10 = befr10 - le10;
                    int aftr5 = befr5 - le5;
                    int aftrTotalAmnt = befrTotalAmnt - tblDepositOrder.DepositAmount;
                    senderDrawer.Le500 = 0;
                    senderDrawer.Le200 = aftr200;
                    senderDrawer.Le100 = aftr100;
                    senderDrawer.Le50 = aftr50;
                    senderDrawer.Le20 = aftr20;
                    senderDrawer.Le10 = aftr10;
                    senderDrawer.Le5 = aftr5;
                    senderDrawer.TtlAmntOwned = aftrTotalAmnt;

                    TblDrwerTransAction drwerTransAction = new TblDrwerTransAction()
                    {
                        RefCode = referenceCode,
                        DrawerIdFk = senderDrawer.Id,
                        Own500 = 0,
                        Own200 = befr200,
                        Own100 = befr100,
                        Own50 = befr50,
                        Own20 = befr20,
                        Own10 = befr10,
                        Own5 = befr5,
                        TtlAmntOwned = befrTotalAmnt,
                        Le500 = 0,
                        Le200 = le200 * -1,
                        Le100 = le100 * -1,
                        Le50 = le50 * -1,
                        Le20 = le20 * -1,
                        Le10 = le10 * -1,
                        Le5 = le5 * -1,
                        TtlAmntTrnsAct = tblDepositOrder.DepositAmount * -1,
                        After500 = 0,
                        After200 = aftr200,
                        After100 = aftr100,
                        After50 = aftr50,
                        After20 = aftr20,
                        After10 = aftr10,
                        After5 = aftr5,
                        TtlAmntAfter = aftrTotalAmnt,
                        TransDate = transDate,
                        DateAndTime = dateAndTime,
                        TransTypeId = 3, // Transaction Type Deposit
                        Description = "توريد / تسديد"
                    };

                    TblRegistration tblRegistratiion = null;
                    TblBlncTrnsAct tblBlncTrnsAct = null;
                    TblAccount depositToAcc = null;
                    int? dpstFromDbtBfr = 0;
                    int? dpstFromDbtAftr = 0;
                    //tblPayTrnx payTrnx = null;

                    if (depositType == 1)
                    {
                        tblRegistratiion = await _context.TblRegistrations.FirstOrDefaultAsync(r => r.UserFk == 8 && r.ProviderFk == tblDepositOrder.ProviderIdFk);
                        var balanceBfr = tblRegistratiion.Balance;///////////Add Product Services Error/////////////////////
                        var balanceAftr = balanceBfr + depositAmount;
                        tblRegistratiion.Balance = balanceAftr;
                        int registerId = tblRegistratiion.Id; // Just For Balance 

                        tblBlncTrnsAct = new TblBlncTrnsAct()
                        {
                            ReferenceCode = referenceCode,
                            SendrUserId = currentUserId,
                            SendrRegId = registerId,
                            RecvrUserId = receiverId ?? 1,
                            RecvrRegId = registerId,
                            BlncTrnsfr = depositAmount,
                            SendrBlncBfr = 0,
                            SendrBlncAftr = 0,
                            RecvrBlncBfr = balanceBfr,
                            RecvrBlncAftr = balanceAftr,
                            TransDate = transDate,
                            DateAndTime = dateAndTime,
                            UserFk = currentUserId,
                            ProdidFk = 1,
                            TransTypeId = 3,
                            Description = "توريد / تسديد"
                        };
                    }
                    else
                    {
                        depositToAcc = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == depositFor);
                        dpstFromDbtBfr = depositToAcc.Indebtedness;/////////////////
                        dpstFromDbtAftr = dpstFromDbtBfr + depositAmount;
                        depositToAcc.Indebtedness = dpstFromDbtAftr;
                    }

                    TblAccount senderAcc = await _context.TblAccounts.SingleOrDefaultAsync(a => a.UserIdFk == deposer);
                    // تأكد من أن الكائنات ليست null قبل استخدامها
                    if (senderAcc == null /* || other required objects are null */)
                    {
                        throw new InvalidOperationException("بيانات الحسابات غير مكتملة.");
                    }
                    int? debtBfor = senderAcc.Indebtedness;
                    int? debtAftr = debtBfor + depositAmount;
                    senderAcc.Indebtedness = debtAftr;

                    TblDebtTrnsAct debtTrnsAct = new TblDebtTrnsAct()
                    {
                        ReferenceCode = referenceCode,
                        AccidColToFk = (depositToAcc != null) ? depositToAcc.Id : 1,
                        AccidColFromFk = senderAcc.Id,
                        AmntTrnsAct = depositAmount * -1,
                        BlncTrnsAct = 0,
                        ColToDbtBfr = debtBfor,
                        ColToDbtAftr = debtAftr,
                        ColFrmDbtBefr = (depositType != 1) ? dpstFromDbtBfr : 0,
                        ColfrmDebtAfter = (depositType != 1) ? dpstFromDbtAftr : 0,
                        TransDate = transDate,
                        DateAndTime = dateAndTime,
                        UserFk = currentUserId,
                        ProvidFk = 1,/////////////////////////////////////////////////
                        TransTypeId = 3,
                        Description = "توريد / تسديد"
                    };

                    TblDepositTrnx trnx = new TblDepositTrnx()
                    {
                        ReferenceCode = referenceCode,
                        ProviderId = tblDepositOrder.ProviderIdFk,
                        DepositAccNo = tblDepositOrder.DepositAccNo,
                        Date = transDate,
                        DepositPhoto = tblDepositOrder.DepositPhoto,
                        DepositAmount = depositAmount,
                        Le500 = 0,
                        Le200 = le200,
                        Le100 = le100,
                        Le50 = le50,
                        Le20 = le20,
                        Le10 = le10,
                        Le5 = le5,
                        UserIdFk = currentUserId,
                        DepositFor = depositFor,
                        DepositType = tblDepositOrder.DepositType,
                        StatusIdFk = 2,
                        Description = "توريد / تسديد"
                    };

                    // *** التصحيح: طريقة التعامل مع Session في Controller
                    if (debtAftr.HasValue)
                    {
                        HttpContext.Session.SetString("Indebt", debtAftr.Value.ToString());
                    }

                    // Database Connection
                    _context.Entry(senderAcc).State = EntityState.Modified;
                    _context.Entry(senderDrawer).State = EntityState.Modified;
                    _context.TblDrwerTransActions.Add(drwerTransAction);
                    _context.Entry(tblDepositOrder).State = EntityState.Modified;
                    if (depositType == 1)
                    {
                        _context.Entry(tblRegistratiion).State = EntityState.Modified;
                        _context.TblBlncTrnsActs.Add(tblBlncTrnsAct);
                    }
                    else
                    {
                        _context.Entry(depositToAcc).State = EntityState.Modified;
                    }
                    _context.TblDebtTrnsActs.Add(debtTrnsAct);
                    _context.TblDepositTrnxes.Add(trnx);
                    await _context.SaveChangesAsync(); // احفظ كل التغييرات المؤقتة// *** التصحيح: استخدام النسخة الـ async
                    // إذا نجح كل شيء حتى هذه النقطة، قم بتأكيد الـ Transaction
                    await transaction.CommitAsync();
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    // إذا حدث أي خطأ في أي مكان داخل الـ try block
                    // قم بالتراجع عن كل التغييرات التي تمت
                    await transaction.RollbackAsync();

                    // (اختياري ولكن موصى به) سجل الخطأ لتتمكن من تحليله لاحقًا
                    // Log the error (ex.ToString()) using a logging library

                    TempData["ErrorMessage"] = "حدث خطأ غير متوقع أثناء تأكيد العملية. لم يتم إجراء أي تغييرات.";
                    return RedirectToAction("Index");
                }
            }
        }

        // GET: TblDepositOrders/Delete/5
        public async Task<IActionResult> Delete(int? id)
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

            var tblDepositOrder = await _context.TblDepositOrders
                .Include(t => t.DepositForNavigation)
                .Include(t => t.ProviderIdFkNavigation)
                .Include(t => t.Status)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDepositOrder == null)
            {
                return NotFound();
            }

            return View(tblDepositOrder);
        }

        // POST: TblDepositOrders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblDepositOrder = await _context.TblDepositOrders.FindAsync(id);
            if (tblDepositOrder != null)
            {
                _context.TblDepositOrders.Remove(tblDepositOrder);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblDepositOrderExists(int id)
        {
            return _context.TblDepositOrders.Any(e => e.Id == id);
        }
    }
}
