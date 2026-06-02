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
    public class TblOrdersController : Controller
    {
        private readonly CollectAgentDBContext _context;
        readonly BalanceTransferAction transferAction = new BalanceTransferAction();

        public TblOrdersController(CollectAgentDBContext context)
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

        // GET: TblOrders
        public async Task<IActionResult> Index()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            var collectAgentDBContext = _context.TblOrders.Include(t => t.ProdidFkNavigation).Include(t => t.RegidFkNavigation).Include(t => t.StatusFkNavigation).Include(t => t.UserFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (id == null)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            var tblOrder = await _context.TblOrders
                .Include(t => t.ProdidFkNavigation)
                .Include(t => t.RegidFkNavigation)
                .Include(t => t.StatusFkNavigation)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblOrder == null)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            return View(tblOrder);
        }

        // GET: TblOrders/BalanceTransfer/5
        public async Task<IActionResult> BalanceTransfer(int id) // 'id' هنا هو User ID الخاص بالمستقبل
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            // --- (تم التعديل) ---
            // البحث عن حساب المستقبل مع تضمين بيانات المستخدم المرتبطة به
            var receiverAccount = await _context.TblAccounts
                                                .Include(a => a.UserIdFkNavigation) // مهم لتجنب NullReferenceException عند الوصول لبيانات المستخدم
                                                .FirstOrDefaultAsync(a => a.UserIdFk == id && a.IsActive == true);

            // التحقق من وجود حساب للمستقبل
            if (receiverAccount == null)
            {
                TempData["ErrorMessage"] = "خطأ! لم يتم العثور على حساب نشط لهذا التاجر.";
                return RedirectToAction(nameof(Index), "TblUsers", new { mode = "oprate" }); // العودة للصفحة الرئيسية للطلبات
            }

            // 1. جلب كل نقاط البيع (الحسابات) النشطة الخاصة بالمستقبل
            var receiverRegistrations = await _context.TblRegistrations
            .Where(r => r.UserFk == id && r.IsActive == true)
            .Include(r => r.ProviderFkNavigation) // لجلب اسم المورد
            .ToListAsync();

            // التحقق من وجود نقطة بيع واحدة على الأقل
            if (!receiverRegistrations.Any())
            {
                TempData["ErrorMessage"] = "خطأ! التاجر ليس لديه أي نقاط بيع نشطة للتحويل إليها.";
                return RedirectToAction(nameof(Index), "TblUsers", new { mode = "oprate" });
            }

            // 2. تجهيز القائمة المنسدلة للعرض في الواجهة
            // سنعرض اسم الحساب + اسم المورد لتمييز الحسابات
            ViewBag.ReceiverRegistrations = new SelectList(
                receiverRegistrations,
                "Id", // القيمة التي سترسل (ID نقطة البيع)
                "AccNameNo", // النص الذي سيظهر للمستخدم
                null, // القيمة المختارة افتراضيًا (لا شيء)
                "ProviderFkNavigation.ProviderName" // تجميع الحسابات حسب اسم المورد
            );

            // التحقق من أن المرسل نفسه لديه صلاحية التحويل (لديه نقطة بيع)
            TblRegistration? senderReg = await _context.TblRegistrations.FirstOrDefaultAsync(s => s.UserFk == currentUserId);
            if (senderReg == null)
            {
                TempData["ErrorMessage"] = "خطأ! حسابك لا يمتلك صلاحية التحويل. يرجى مراجعة مدير النظام.";
                return RedirectToAction(nameof(Index), "TblUsers", new { mode = "oprate" });
            }

            // إنشاء كائن جديد داخل الدالة بدلاً من استخدام الكائن المعرف على مستوى الكلاس
            var transferActionModel = new BalanceTransferAction
            {
                ReceiverDebtBefore = receiverAccount.Indebtedness ?? 0,
                ReceiverUserId = receiverAccount.UserIdFk,
                SenderUserId = currentUserId
            };

            // استدعاء الدالة المساعدة لتجهيز الصفحة
            return await PrepareAndReturnView(transferActionModel);
        }

        // POST: TblOrders/BalanceTransfer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BalanceTransfer(BalanceTransferAction balanceTransferAction)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            // التحقق من أن النموذج صالح

            if (!ModelState.IsValid)
            {
                return await PrepareAndReturnView(balanceTransferAction);
            }

            //if (!ModelState.IsValid)
            //{
            //    // يجب إعادة ملء البيانات اللازمة لعرض الـ View مرة أخرى في حالة الخطأ
            //    TblAccount? currentReceiverAcc = await _context.TblAccounts
            //        .Include(a => a.UserIdFkNavigation) // تضمين بيانات المستخدم
            //        .FirstOrDefaultAsync(a => a.UserIdFk == balanceTransferAction.ReceiverUserId);

            //    if (currentReceiverAcc != null)
            //    {
            //        balanceTransferAction.ReceiverDebtBefore = (int)(currentReceiverAcc.Indebtedness ?? 0);
            //        // --- (تم التعديل) --- استخدام الخاصية الصحيحة من الموديل
            //        ViewBag.shopname = currentReceiverAcc.UserIdFkNavigation?.FullName;
            //    }
            //    return View(balanceTransferAction);
            //}

            //// --- (تم التعديل) ---
            //// تحويل قيم الجلسة من string إلى int
            ////int senderID = currentUserId;

            //// استخدام DateTime.Now لتجنب قيمة null إذا لم يتم إرسالها من النموذج
            //balanceTransferAction.dateTime = balanceTransferAction.dateTime == DateTime.MinValue ? DateTime.Now : balanceTransferAction.dateTime;
            //balanceTransferAction.dateOnly = DateOnly.FromDateTime(balanceTransferAction.dateTime);

            //DateOnly transDate = balanceTransferAction.dateOnly;
            //DateTime dateAndTime = balanceTransferAction.dateTime;
            //var referanceCode = dateAndTime.ToString("yyMMddHHmmssff");

            //int blncTrnsact = balanceTransferAction.BlncTrnsActAmount;

            //// --- (تعديل 2) ---
            //// جلب نقطة بيع المستقبل بناءً على اختيار المستخدم
            //TblRegistration receiverReg = await _context.TblRegistrations
            //    .FirstOrDefaultAsync(r => r.Id == balanceTransferAction.SelectedReceiverRegId);

            //if (receiverReg == null)
            //{
            //    ModelState.AddModelError("", "حساب المستقبل المختار غير صالح.");
            //    return await PrepareAndReturnView(balanceTransferAction);
            //}

            //// البحث عن حساب المستقبل
            //TblAccount? receiverAcc = _context.TblAccounts
            //    .Include(a => a.UserIdFkNavigation) // تضمين بيانات المستخدم
            //    .FirstOrDefault(u => u.UserIdFk == balanceTransferAction.ReceiverUserId);

            //if (receiverAcc == null)
            //{
            //    ViewBag.Error = "لم يتم العثور على حساب المستقبل. يرجى إضافة حساب للمستقبل";
            //    return View(balanceTransferAction);
            //}

            //// --- (تعديل 3) ---
            //// جلب نقطة بيع المرسل التي تطابق مورد نقطة بيع المستقبل
            //TblRegistration senderReg = await _context.TblRegistrations
            //    .FirstOrDefaultAsync(s => s.UserFk == currentUserId && s.ProviderFk == receiverReg.ProviderFk);

            //if (senderReg == null)
            //{
            //    ModelState.AddModelError("", $"ليس لديك حساب للمورد المحدد ({receiverReg.ProviderFkNavigation.ProviderName}). لا يمكن إتمام التحويل.");
            //    return await PrepareAndReturnView(balanceTransferAction);
            //}

            //// البحث عن سجل (Registration) المستقبل
            ////TblRegistration? receiverReg = _context.TblRegistrations.FirstOrDefault(r => r.UserFk == balanceTransferAction.ReceiverUserId);
            ////if (receiverReg == null)
            ////{
            ////    ViewBag.Error = "لا يوجد نقطة بيع للمستقبل. يرجى إضافة نقطة بيع.";
            ////    return View(balanceTransferAction);
            ////}

            //var recvrBlncBefor = receiverReg.Balance;
            //// معرفة نوع المستخدم لمستقبل الرصيد
            //TblUser? receiverUser = _context.TblUsers.FirstOrDefault(u => u.Id == balanceTransferAction.ReceiverUserId);
            //if (receiverUser != null && receiverUser.UserTypeFk < 8)
            //{
            //    receiverReg.Balance += blncTrnsact;
            //}

            //var receiverDebtBefore = receiverAcc.Indebtedness ?? 0;
            //var receiverDebtAfter = receiverDebtBefore + blncTrnsact;
            //receiverAcc.Indebtedness = receiverDebtAfter;

            //TblRegistration? senderReg = _context.TblRegistrations.FirstOrDefault(s => s.UserFk == currentUserId);
            //if (senderReg == null)
            //{
            //    ViewBag.Error = "لم يتم العثور على سجل المرسل.";
            //    return View(balanceTransferAction);
            //}

            //var sendrBlncBfor = senderReg.Balance;
            //senderReg.Balance -= blncTrnsact; // الرصيد يجب أن ينقص من المرسل وليس يزيد

            //// التحقق من الرصيد الكافي
            //if (senderReg.Balance < 0)
            //{
            //    // 1. أرجع الرصيد إلى قيمته الأصلية لأن العملية فشلت
            //    senderReg.Balance = sendrBlncBfor;

            //    // 2. ضع رسالة الخطأ التي ستظهر للمستخدم
            //    ViewBag.Error = "لا يوجد رصيد كافٍ في حسابك لإتمام العملية.";

            //    // 3. أعد تعبئة اسم المحل للعرض مرة أخرى في رأس الصفحة
            //    // (هذه الخطوة مهمة جدًا، بدونها سيكون رأس الصفحة فارغًا)
            //    ViewBag.shopname = receiverAcc.UserIdFkNavigation?.FullName;

            //    balanceTransferAction.ReceiverDebtBefore = (int)receiverDebtBefore;

            //    // 4. أعد الموديل إلى الصفحة لعرض البيانات التي أدخلها المستخدم
            //    return View(balanceTransferAction);
            //}

            //// --- (تم التعديل) ---
            //// الطريقة الصحيحة لتخزين قيمة في الجلسة
            //HttpContext.Session.SetString("Balance", senderReg.Balance.ToString());

            //TblAccount? senderAcc = _context.TblAccounts.FirstOrDefault(u => u.UserIdFk == currentUserId);

            //TblBlncTrnsAct BlncTrnsAct = new TblBlncTrnsAct()
            //{
            //    ReferenceCode = referanceCode,
            //    SendrUserId = senderAcc.UserIdFk,
            //    SendrRegId = senderReg.Id,
            //    RecvrUserId = receiverAcc.UserIdFk,
            //    RecvrRegId = receiverReg.Id,
            //    BlncTrnsfr = blncTrnsact,
            //    SendrBlncBfr = sendrBlncBfor,
            //    SendrBlncAftr = senderReg.Balance,
            //    TransDate = transDate,
            //    DateAndTime = dateAndTime,
            //    ProdidFk = balanceTransferAction.ProductID,
            //    UserFk = currentUserId,
            //    TransTypeId = 1,
            //    Description = " تحويل رصيد لتاجر "
            //};

            //// لا داعي للتحقق من ModelState.IsValid مرة أخرى هنا
            //_context.TblBlncTrnsActs.Add(BlncTrnsAct);
            //_context.TblDebtTrnsActs.Add(BlncDebtTrnsAct);
            //_context.Entry(senderReg).State = EntityState.Modified;
            //_context.Entry(receiverAcc).State = EntityState.Modified;
            //_context.Entry(receiverReg).State = EntityState.Modified;
            //_context.SaveChanges();

            //TempData["Whatsapp"] = true;
            //return RedirectToAction("Index");

            // ====================================================================
            // *** إضافة هذا السطر كخط دفاع (Fallback) ***
            // إذا كان التاريخ المرسل هو القيمة الافتراضية (0001)، استخدم وقت السيرفر
            if (balanceTransferAction.dateTime == DateTime.MinValue)
            {
                balanceTransferAction.dateTime = DateTime.Now;
            }
            // ====================================================================

            // 1. جلب نقطة بيع المستقبل (مع المورد) بناءً على اختيار المستخدم
            var receiverReg = await _context.TblRegistrations
                .Include(r => r.ProviderFkNavigation) // مهم لرسالة الخطأ
                .FirstOrDefaultAsync(r => r.Id == balanceTransferAction.SelectedReceiverRegId);

            if (receiverReg == null)
            {
                ModelState.AddModelError("SelectedReceiverRegId", "حساب المستقبل المختار غير صالح.");
                return await PrepareAndReturnView(balanceTransferAction);
            }

            // 2. جلب نقطة بيع المرسل التي تطابق مورد نقطة بيع المستقبل
            var senderReg = await _context.TblRegistrations
                .FirstOrDefaultAsync(s => s.UserFk == currentUserId && s.ProviderFk == receiverReg.ProviderFk);

            if (senderReg == null)
            {
                ModelState.AddModelError("", $"ليس لديك حساب للمورد المحدد ({receiverReg.ProviderFkNavigation.ProviderName}). لا يمكن إتمام التحويل.");
                return await PrepareAndReturnView(balanceTransferAction);
            }

            // 3. جلب الحسابات العامة للمرسل والمستقبل (TblAccount)
            var receiverAcc = await _context.TblAccounts.FirstOrDefaultAsync(u => u.UserIdFk == balanceTransferAction.ReceiverUserId);
            var senderAcc = await _context.TblAccounts.FirstOrDefaultAsync(u => u.UserIdFk == currentUserId);
            if (receiverAcc == null || senderAcc == null)
            {
                ViewBag.Error = "خطأ في العثور على حسابات المرسل أو المستقبل.";
                return await PrepareAndReturnView(balanceTransferAction);
            }

            // --- تنفيذ منطق التحويل ---
            int blncTrnsact = balanceTransferAction.BlncTrnsActAmount;
            var sendrBlncBfor = senderReg.Balance;

            senderReg.Balance -= blncTrnsact;

            // التحقق من الرصيد الكافي
            if (senderReg.Balance < 0)
            {
                ModelState.AddModelError("", "لا يوجد رصيد كافٍ في حسابك لهذا المورد لإتمام العملية.");
                return await PrepareAndReturnView(balanceTransferAction); // سيعيد الصفحة مع رسالة الخطأ
            }

            if (senderReg.Balance >= 0)
            {
                // الطريقة الصحيحة لتخزين قيمة في الجلسة
                HttpContext.Session.SetString("Balance", senderReg.Balance.ToString());
            }

            //var senderDebtBefore = senderAcc.Indebtedness ?? 0;
            //var senderDebtAfter = senderDebtBefore - blncTrnsact;
            //senderAcc.Indebtedness = senderDebtAfter;

            var receiverType = await _context.TblUsers
                .Where(u => u.Id == balanceTransferAction.ReceiverUserId)
                .Select(u => u.UserTypeFk)
                .FirstOrDefaultAsync();
            var recvrBlncBefor = 0;
            var recvrBlncAftr = 0;
            // فقط إذا كان نوع المستخدم أقل من 8 (تاجر/مندوب)  
            if (receiverType < 8)
            {
                recvrBlncBefor = receiverReg.Balance;
                receiverReg.Balance += blncTrnsact;
                recvrBlncAftr = receiverReg.Balance;
            }

            var receiverDebtBefore = receiverAcc.Indebtedness ?? 0;
            var receiverDebtAfter = receiverDebtBefore + blncTrnsact;
            receiverAcc.Indebtedness = receiverDebtAfter;

            // --- إنشاء سجلات الحركة ---
            var transDate = DateOnly.FromDateTime(balanceTransferAction.dateTime);
            var dateAndTime = balanceTransferAction.dateTime;
            var referanceCode = dateAndTime.ToString("yyMMddHHmmssff");

            var blncDebtTrnsAct = new TblDebtTrnsAct
            {
                UserFk = currentUserId,
                ReferenceCode = referanceCode,
                AccidColToFk = senderAcc.Id,
                AccidColFromFk = receiverAcc.Id,
                AmntTrnsAct = 0,
                BlncTrnsAct = blncTrnsact,
                ColToDbtBfr = senderAcc.Indebtedness,
                ColToDbtAftr = senderAcc.Indebtedness,
                ColFrmDbtBefr = receiverDebtBefore,
                ColfrmDebtAfter = receiverDebtAfter,
                TransDate = transDate,
                DateAndTime = dateAndTime,
                ProvidFk = receiverReg.ProviderFk,
                TransTypeId = 1,
                Description = "تحويل رصيد لتاجر/مندوب"
            };

            // --- (تم التعديل) --- استخدام الخاصية الصحيحة
            var blncTrnsAct = new TblBlncTrnsAct
            {
                UserFk = currentUserId,
                ReferenceCode = referanceCode,
                SendrUserId = senderAcc.UserIdFk,
                SendrRegId = senderReg.Id,
                RecvrUserId = receiverAcc.UserIdFk,
                RecvrRegId = receiverReg.Id,
                BlncTrnsfr = blncTrnsact,
                SendrBlncBfr = sendrBlncBfor,
                SendrBlncAftr = senderReg.Balance,
                RecvrBlncBfr = recvrBlncBefor,
                RecvrBlncAftr = recvrBlncAftr,
                TransDate = transDate,
                DateAndTime = dateAndTime,
                ProdidFk = 1, // balanceTransferAction.ProductID
                TransTypeId = 1,
                Description = "تحويل رصيد لتاجر/مندوب"
            };

            _context.TblBlncTrnsActs.Add(blncTrnsAct);
            _context.TblDebtTrnsActs.Add(blncDebtTrnsAct);
            // لا حاجة لاستخدام .State = Modified، EF Core يتتبع التغييرات تلقائيًا
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحويل الرصيد بنجاح.";
            return RedirectToAction(nameof(Index), "TblUsers", new { mode = "oprate" }); // أو أي صفحة نجاح أخرى
        }

        // --- (إضافة جديدة) ---
        // دالة مساعدة لتجهيز الصفحة بالبيانات اللازمة وتجنب تكرار الكود
        [NonAction]
        private async Task<IActionResult> PrepareAndReturnView(BalanceTransferAction model)
        {
            var user = await _context.TblUsers.FindAsync(model.ReceiverUserId);
            ViewBag.shopname = !string.IsNullOrEmpty(user?.UserCode) ? user.UserCode : user?.FullName;

            var receiverRegistrations = await _context.TblRegistrations
                .Where(r => r.UserFk == model.ReceiverUserId && r.IsActive == true)
                .Include(r => r.ProviderFkNavigation)
                .ToListAsync();

            ViewBag.ReceiverRegistrations = new SelectList(
                receiverRegistrations,
                "Id",
                "AccNameNo",
                model.SelectedReceiverRegId,
                "ProviderFkNavigation.ProviderName"
            );

            return View(model);
        }

        // GET: TblOrders/Create
        public IActionResult Create()
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
               TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }
            ViewData["ProdidFk"] = new SelectList(_context.TblProducts, "Id", "ProductCode");
            ViewData["RegidFk"] = new SelectList(_context.TblRegistrations, "Id", "AccNameNo");
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName");
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            return View();
        }

        // POST: TblOrders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserFk,OrderNo,RegidFk,ProdidFk,IndebtBefore,BlncAmntOrder,IndebtAfter,TransDate,DateAndTime,StatusFk")] TblOrder tblOrder)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                _context.Add(tblOrder);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), "TblUsers");
            }
            ViewData["ProdidFk"] = new SelectList(_context.TblProducts, "Id", "ProductCode", tblOrder.ProdidFk);
            ViewData["RegidFk"] = new SelectList(_context.TblRegistrations, "Id", "AccNameNo", tblOrder.RegidFk);
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblOrder.StatusFk);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblOrder.UserFk);
            return View(tblOrder);
        }

        // GET: TblOrders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (id == null)
            {
                TempData["ErrorMessage"] = "مستخدم غير موجود !!!";
                return RedirectToAction("Index", "Home");
            }

            var tblOrder = await _context.TblOrders.FindAsync(id);
            if (tblOrder == null)
            {
                TempData["ErrorMessage"] = " بيانات غير موجودة !!!";
                return RedirectToAction("Index", "Home");
            }
            ViewData["ProdidFk"] = new SelectList(_context.TblProducts, "Id", "ProductCode", tblOrder.ProdidFk);
            ViewData["RegidFk"] = new SelectList(_context.TblRegistrations, "Id", "AccNameNo", tblOrder.RegidFk);
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblOrder.StatusFk);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblOrder.UserFk);
            return View(tblOrder);
        }

        // POST: TblOrders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserFk,OrderNo,RegidFk,ProdidFk,IndebtBefore,BlncAmntOrder,IndebtAfter,TransDate,DateAndTime,StatusFk")] TblOrder tblOrder)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (id != tblOrder.Id)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblOrder);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblOrderExists(tblOrder.Id))
                    {
                        TempData["ErrorMessage"] = " بيانات غير موجودة !!!";
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index), "TblUsers");
            }
            ViewData["ProdidFk"] = new SelectList(_context.TblProducts, "Id", "ProductCode", tblOrder.ProdidFk);
            ViewData["RegidFk"] = new SelectList(_context.TblRegistrations, "Id", "AccNameNo", tblOrder.RegidFk);
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblOrder.StatusFk);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblOrder.UserFk);
            return View(tblOrder);
        }

        // GET: TblOrders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();
            if (currentUserType > 5)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }
            if (id == null)
            {
                TempData["ErrorMessage"] = "مستخدم غير موجود !!!";
                return RedirectToAction("Index", "Home");
            }

            var tblOrder = await _context.TblOrders
                .Include(t => t.ProdidFkNavigation)
                .Include(t => t.RegidFkNavigation)
                .Include(t => t.StatusFkNavigation)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblOrder == null)
            {
                TempData["ErrorMessage"] = "بيانات غير موجودة !!!";
                return RedirectToAction("Index", "Home");
            }

            return View(tblOrder);
        }

        // POST: TblOrders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblOrder = await _context.TblOrders.FindAsync(id);
            if (tblOrder != null)
            {
                _context.TblOrders.Remove(tblOrder);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), "TblUsers");
        }

        private bool TblOrderExists(int id)
        {
            return _context.TblOrders.Any(e => e.Id == id);
        }
    }
}
