using CollectAgent.Models;
using CollectAgent.ViewModels;
using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authorization; // أضف هذا
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims; // أضف هذا للوصول إلى بيانات المستخدم
using System.Threading.Tasks;
using X.PagedList;
using X.PagedList.Extensions; // ستحتاج لتثبيت هذه الحزمة: Install-Package X.PagedList.Mvc.Core

namespace CollectAgent.Controllers
{
    [Authorize]  // استخدم هذا السطر ليتم التحقق من تسجيل الدخول تلقائياً
    public class TblServTrnxesController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblServTrnxesController(CollectAgentDBContext context)
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

        // GET: TblServTrnxes
        public async Task<IActionResult> Index(string searchTerm, DateTime? startDate, DateTime? endDate, int? page)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            // ابدأ ببناء الاستعلام الأساسي دون تنفيذه
            var query = _context.TblServTrnxes.Where(t => t.SenderUserId == currentUserId)
                                .Include(t => t.RecvrUser)
                                .Include(t => t.SenderUser)
                                .Include(t => t.ServProdFkNavigation).ThenInclude(p => p.ServProvFkNavigation)
                                .Include(t => t.StatusFkNavigation)
                                .AsQueryable(); // مهم جداً لتحسين الأداء
            //var collectAgentDBContext = _context.TblServTrnxes.Include(t => t.RecvrUser).Include(t => t.SenderUser).Include(t => t.SendrReg).Include(t => t.ServProdFkNavigation).Include(t => t.StatusFkNavigation);

            // 1. تطبيق فلتر البحث
            if (!string.IsNullOrEmpty(searchTerm))
            {
                // يبحث في كود المرجع أو رقم العميل
                query = query.Where(t => t.ReferenceCode.Contains(searchTerm) || t.CustomerNoTrx.Contains(searchTerm));
            }

            // 2. تطبيق فلتر التاريخ
            if (startDate.HasValue)
            {
                query = query.Where(t => t.DateAndTime >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                // أضفنا يومًا واحدًا لنهاية التاريخ ليشمل كل ساعات اليوم الأخير
                query = query.Where(t => t.DateAndTime < endDate.Value.AddDays(1));
            }

            // ترتيب السجلات من الأحدث للأقدم
            query = query.OrderByDescending(t => t.DateAndTime);

            // 3. تطبيق ترقيم الصفحات (Pagination)
            int pageNumber = page ?? 1;
            int pageSize = 15; // يمكنك تغيير عدد العناصر في الصفحة

            var pagedTransactions = query.ToPagedList(pageNumber, pageSize);
            
            // تمرير قيم الفلاتر للـ View للاحتفاظ بها في حقول البحث
            ViewBag.CurrentSearch = searchTerm;
            ViewBag.CurrentStartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentEndDate = endDate?.ToString("yyyy-MM-dd");

            // التحقق إذا كان الطلب من نوع AJAX
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // إذا كان كذلك، أرجع الـ Partial View فقط
                return PartialView("_TransactionListPartial", pagedTransactions);
            }

            // إذا كان طلب عادي، أرجع الصفحة الكاملة
            return View(pagedTransactions);
        }

        // GET: TblServTrnxes/Details/5
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

            var tblServTrnx = await _context.TblServTrnxes
                .Include(t => t.RecvrUser)
                .Include(t => t.SenderUser)
                .Include(t => t.SendrReg)
                .Include(t => t.ServProdFkNavigation)
                .Include(t => t.StatusFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblServTrnx == null)
            {
                return NotFound();
            }

            return View(tblServTrnx);
        }

        // GET: TblServTrnxes/PreCreate
        [Authorize] // يضمن أن المستخدم مسجل دخوله قبل الوصول لهذا الـ Action
        public async Task<IActionResult> PreCreate(int? providerID, int? prodRegID, int? merchantId)
        {
            var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            // 1. التحقق من المدخلات
            if (providerID == null || providerID <= 0)
            {
                // إذا لم يتم توفير معرف المزوّد، أرجع خطأ
                return BadRequest("Provider ID is required.");
            }

            // --- البديل الحديث والآمن لـ Session ---
            // جلب بيانات المستخدم الحالي من الـ Claims التي تم تخزينها عند تسجيل الدخول
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // هذا يجلب ID المستخدم
                                                                               // لنفترض أن لديك merchantID و prodRegID كـ Claims أيضاً
            if (merchantId.HasValue)
            {
                ViewBag.MerchantName = (await _context.TblUsers.FindAsync(merchantId.Value))?.FullName;
            }
            //var prodRegId = User.FindFirstValue("ProdRegId");

            // تحقق من أن البيانات موجودة
            if (currentUserId == null || prodRegID == null)
            {
                // إذا كانت بيانات المستخدم غير كاملة، يمكن إعادته لصفحة الدخول أو عرض خطأ
                return Unauthorized("Erorr 2001 , خطأ مستخدم أو حساب");
            }

            // 2. استخدام async/await و ViewModel و Enum
            try
            {
                // جلب المنتجات بشكل غير متزامن
                var productsFromDb = await _context.TblServProducts
                    .Include(p => p.ServProvFkNavigation) // لجلب اسم المزوّد
                    .Where(p => p.ServProvFk == providerID &&
                                 p.CategoryFk == 1 /*(int)ProductCategory.RechargeCards*/ && // 3. استخدام الـ Enum
                                 p.IsActive)
                    .Select(p => new ServiceProductCardViewModel
                    {
                        ProductId = p.Id,
                        ProductName = p.ServProdName,
                        SellPrice = p.ServProdSellPrice, // <-- تعبئة السعر
                        ProviderId = p.ServProvFk,
                        // يمكنك إضافة الشعار إذا أردت
                        ProviderLogoUrl = p.ServProvFkNavigation.ProviderLogo
                    })
                                 .ToListAsync(); // من الجيد التأكد من أن المنتج نشط

                // جلب اسم مزود الخدمة
                var provider = await _context.TblProviders.FindAsync(providerID);

                // جلب اسم التاجر (إذا كان موجوداً)
                string? merchantName = null;
                if (merchantId.HasValue)
                {
                    var merchantUser = await _context.TblUsers.FindAsync(merchantId.Value);
                    merchantName = merchantUser?.FullName;
                }

                // إنشاء الـ ViewModel الرئيسي
                var viewModel = new PreCreateViewModel
                {
                    PageTitle = "اختر الخدمة المطلوبة",
                    ProviderName = provider?.ProviderName ?? "مزود الخدمة",
                    MerchantName = merchantName,
                    Products = productsFromDb,
                    // بناء رابط الرجوع باستخدام بيانات المستخدم من الـ Claims
                    BackButtonUrl = Url.Action("CashBalance", "Home", new { senderid = userIdString, provType = 3 }),
                };
                // تمرير الـ merchantId و prodRegId للـ View بطريقة آمنة
                ViewData["MerchantId"] = merchantId;
                ViewData["ProdRegId"] = prodRegID;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // يمكنك تسجيل الخطأ (logging)
                // وإرجاع صفحة خطأ مخصصة
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // GET: TblServTrnxes/Create/{productId}/{merchantId?}
        [HttpGet("TblServTrnxes/Create/{productId}/{merchantId?}")]
        public async Task<IActionResult> Create(int productId, int? merchantId)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();

            // 1. جلب بيانات المنتج
            var product = await _context.TblServProducts
                                        .Include(p => p.ServProvFkNavigation)
                                        .FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null) return NotFound("المنتج غير موجود.");

            // Replace all instances of: product.TypeFk ?? 1
            // with: product.TypeFk != 0 ? product.TypeFk : 1

            // Example fix in the Create GET action:
            var viewModel = new ServTrnxCreateViewModel
            {
                ServProdFk = productId,
                SenderUserId = currentUserId,
                BlncAmntOrderTrx = 0,
                // 1 = إيداع (الوكيل يدفع رصيد ويقبض كاش/مديونية)
                // 2 = سحب (الوكيل يقبض رصيد ويدفع كاش/يخصم مديونية)
                ProductType = product.TypeFk != 0 ? product.TypeFk : 1,
                SellPriceTrx = (int)Math.Ceiling(product.ServProdSellPrice ?? 0)
            };

            string headerName;

            // 3. تحديد السيناريو (آجل أم كاش)
            if (merchantId.HasValue && merchantId.Value > 0)
            {
                // === سيناريو الآجل (للتاجر) ===
                var merchantAccount = await _context.TblAccounts
                                                    .Include(a => a.UserIdFkNavigation) // تصحيح: العلاقة مع User عادة تكون UserIdFkNavigation أو UserFkNavigation حسب الموديل
                                                    .FirstOrDefaultAsync(a => a.UserIdFk == merchantId.Value);

                if (merchantAccount == null) return NotFound("حساب التاجر غير موجود.");

                viewModel.MerchantId = merchantId.Value;
                viewModel.IndebtBefore = merchantAccount.Indebtedness ?? 0;

                // محاولة جلب الاسم بأمان حسب هيكل الموديل لديك
                var merchantUser = await _context.TblUsers.FindAsync(merchantId.Value);
                headerName = merchantUser?.FullName ?? "تاجر غير معروف";

                // تصفير الفئات النقدية لأن التعامل آجل
                viewModel.Le200 = 0; viewModel.Le100 = 0; viewModel.Le50 = 0;
                viewModel.Le20 = 0; viewModel.Le10 = 0; viewModel.Le5 = 0;
            }
            else
            {
                // === سيناريو الكاش (للدرج) ===
                viewModel.MerchantId = 0;
                viewModel.IndebtBefore = 0;

                // جلب بيانات الدرج (TblDrawer) للمستخدم الحالي
                var userDrawer = await _context.TblDrawers.FirstOrDefaultAsync(d => d.UserFk == currentUserId);

                if (userDrawer != null)
                {
                    // نملأ الـ ViewModel بما هو متاح في الدرج حالياً
                    // هذا سيفيدنا في الـ View لتحديد الحد الأقصى في حالة "السحب" (Type 2)
                    // حيث لا يستطيع الوكيل إخراج كاش أكثر مما يملك
                    viewModel.Le500 = userDrawer.Le500 ?? 0;
                    viewModel.Le200 = userDrawer.Le200 ?? 0;
                    viewModel.Le100 = userDrawer.Le100 ?? 0;
                    viewModel.Le50 = userDrawer.Le50 ?? 0;
                    viewModel.Le20 = userDrawer.Le20 ?? 0;
                    viewModel.Le10 = userDrawer.Le10 ?? 0;
                    viewModel.Le5 = userDrawer.Le5 ?? 0;
                }
                else
                {
                    // لا يوجد درج بعد، الرصيد النقدي صفر
                    viewModel.Le500 = 0;
                    viewModel.Le200 = 0; viewModel.Le100 = 0; viewModel.Le50 = 0;
                    viewModel.Le20 = 0; viewModel.Le10 = 0; viewModel.Le5 = 0;
                }

                var senderUser = await _context.TblUsers.FindAsync(currentUserId);
                headerName = senderUser?.UserCode ?? "عملية نقدية مباشرة";
            }

            ViewData["Title"] = (viewModel.MerchantId == 0) ? "تنفيذ عملية نقدية" : "تنفيذ عملية آجلة";
            ViewBag.ProviderName = product.ServProvFkNavigation.ProviderName;
            ViewBag.ProductName = product.ServProdName;
            ViewBag.ProviderLogoUrl = product.ServProvFkNavigation.ProviderLogo;
            ViewBag.ShopName = headerName;

            return View(viewModel);
        }

        // POST: TblServTrnxes/Create
        [HttpPost("TblServTrnxes/Create/{productId}/{merchantId?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServTrnxCreateViewModel viewModel)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7) return Forbid();

            // تأكيد هوية المرسل
            viewModel.SenderUserId = currentUserId;

            // 1. جلب المنتج والمزود
            var product = await _context.TblServProducts
                                        .Include(p => p.ServProvFkNavigation)
                                        .FirstOrDefaultAsync(p => p.Id == viewModel.ServProdFk);

            if (product == null) ModelState.AddModelError("", "المنتج غير موجود.");

            // 2. جلب رصيد الوكيل لدى المزود (المحفظة الالكترونية / انستا / بنك)
            var senderProvReg = await _context.TblServProvRegs
                .FirstOrDefaultAsync(r => r.UserFk == currentUserId && r.ServProvFk == product.ServProvFk);

            if (senderProvReg == null) ModelState.AddModelError("", "ليس لديك حساب لهذا المزوّد.");

            // تحديد نوع العملية: 1=إيداع (بيع رصيد للعميل)، 2=سحب (شراء رصيد من العميل)
            int prodType = product.TypeFk != 0 ? product.TypeFk : 1;

            // التحقق من كفاية رصيد المحفظة (في حالة بيع الرصيد - نوع 1)
            if (prodType == 1 && (senderProvReg?.Balance ?? 0) < viewModel.BlncAmntOrderTrx)
            {
                ModelState.AddModelError("BlncAmntOrderTrx", "رصيد المحفظة لا يكفي لإتمام العملية.");
            }

            // تحديد ما إذا كانت العملية كاش (بيع مباشر)
            // التصحيح: التعامل مع null أو 0
            bool isCashSale = (viewModel.MerchantId == null || viewModel.MerchantId <= 0);

            if (ModelState.IsValid)
            {
                using (var dbTransaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        int senderID = currentUserId;
                        // إذا كاش فالطرف الثاني هو الوكيل نفسه (استلم كاش)، إذا آجل فالطرف الثاني هو التاجر
                        int receiverId = isCashSale ? currentUserId : viewModel.MerchantId;
                        // === منطق الحسابات ===
                        // 1. حركة المزود (المحفظة الالكترونية)
                        // تعتمد على خانة الرصيد (BlncAmntOrderTrx)
                        // Type 1 (إيداع/بيع): رصيد المحفظة يقل (-)
                        // Type 2 (سحب/شراء): رصيد المحفظة يزيد (+)
                        int providerChange = (prodType == 1) ? -viewModel.BlncAmntOrderTrx : viewModel.BlncAmntOrderTrx;

                        // 2. حركة النقدية/المديونية (المقابل)
                        // !!! تعتمد الآن على خانة السعر (TotalPriceTrx) !!!
                        // Type 1 (إيداع/بيع): بعنا رصيد -> قبضنا فلوس (الكاش يزيد +)
                        // Type 2 (سحب/شراء): اشترينا رصيد -> دفعنا فلوس (الكاش يقل -)
                        //int cashOrDebtChange = (prodType == 1) ? viewModel.BlncAmntOrderTrx : -viewModel.BlncAmntOrderTrx;
                        int cashOrDebtChange = (prodType == 1) ? viewModel.TotalPriceTrx : -viewModel.TotalPriceTrx;

                        // حساب إجمالي المبلغ للدين (شامل الرسوم إن وجدت في المستقبل)
                        //int totalAmountForDebt = (prodType == 1) ? viewModel.TotalPriceTrx : -viewModel.TotalPriceTrx;
                        int totalAmountForDebt = cashOrDebtChange; // نفس القيمة

                        // تجهيز التواريخ والأكواد
                        var refCode = DateTime.UtcNow.AddHours(2).ToString("yyMMddHHmmssff");
                        var now = DateTime.UtcNow.AddHours(2);
                        var today = DateOnly.FromDateTime(now);

                        // --- تنفيذ التحديثات ---

                        // أولاً: تحديث رصيد المحفظة/المزود
                        // إذا كان الرصيد فارغاً نعتبره صفر، ثم نجمع عليه قيمة التغيير
                        senderProvReg.Balance = (senderProvReg.Balance ?? 0) + providerChange;
                        _context.TblServProvRegs.Update(senderProvReg);

                        // ثانياً: معالجة المقابل (إما كاش في الدرج أو دين على تاجر)
                        if (isCashSale)
                        {
                            // === هنا الإصلاح الأساسي: جلب الدرج داخل الـ Transaction لضمان التحديث ===
                            var userDrawer = await _context.TblDrawers.FirstOrDefaultAsync(d => d.UserFk == currentUserId);
                            var UserAccount = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == currentUserId);
                            // إنشاء درج جديد فوراً إذا لم يكن موجوداً
                            if (userDrawer == null)
                            {
                                userDrawer = new TblDrawer
                                {
                                    UserFk = currentUserId,
                                    Code = "Drw-" + currentUserId,
                                    IsActive = true,
                                    StartDate = today,
                                    Le500 = 0,
                                    Le200 = 0,
                                    Le100 = 0,
                                    Le50 = 0,
                                    Le20 = 0,
                                    Le10 = 0,
                                    Le5 = 0,
                                    TtlAmntOwned = 0
                                };
                                await _context.TblDrawers.AddAsync(userDrawer);
                                // حفظ مؤقت داخل الترانزاكشن للحصول على ID
                                await _context.SaveChangesAsync();
                            }

                            // التحقق من توفر الكاش في حالة (السحب/الشراء - Type 2) قبل الخصم
                            if (prodType == 2 && (userDrawer.TtlAmntOwned ?? 0) < viewModel.BlncAmntOrderTrx)
                            {
                                throw new Exception("لا يوجد نقدية كافية في الدرج لإتمام عملية السحب (شراء الرصيد).");
                            }

                            // حساب الأرصدة الجديدة للدرج
                            int ownTotal = userDrawer.TtlAmntOwned ?? 0;
                            int afterTotal = ownTotal + cashOrDebtChange; // المعادلة: القديم + (موجب في البيع / سالب في الشراء)

                            // حساب الفئات (تحديث شكلي للفئات بناءً على مدخلات المستخدم)
                            // الإشارة: 1 للزيادة (بيع)، -1 للنقص (شراء)
                            int sign = (prodType == 1) ? 1 : -1;

                            int after500 = (userDrawer.Le500 ?? 0) + (sign * (viewModel.Le500 ?? 0));
                            int after200 = (userDrawer.Le200 ?? 0) + (sign * (viewModel.Le200 ?? 0));
                            int after100 = (userDrawer.Le100 ?? 0) + (sign * (viewModel.Le100 ?? 0));
                            int after50 = (userDrawer.Le50 ?? 0) + (sign * (viewModel.Le50 ?? 0));
                            int after20 = (userDrawer.Le20 ?? 0) + (sign * (viewModel.Le20 ?? 0));
                            int after10 = (userDrawer.Le10 ?? 0) + (sign * (viewModel.Le10 ?? 0));
                            int after5 = (userDrawer.Le5 ?? 0) + (sign * (viewModel.Le5 ?? 0));

                            // تسجيل حركة الدرج
                            var drawerTrx = new TblDrwerTransAction
                            {
                                RefCode = refCode,
                                DrawerIdFk = userDrawer.Id,
                                TransTypeId = (prodType == 1) ? 1 : 2, // 1: وارد، 2: منصرف
                                TransDate = today,
                                DateAndTime = now,
                                Description = (prodType == 1) ? $"بيع رصيد (وارد نقدية) - {product.ServProdName}" : $"شراء رصيد (منصرف نقدية) - {product.ServProdName}",

                                TtlAmntOwned = ownTotal, // الرصيد قبل

                                // تفاصيل الحركة
                                Le500 = viewModel.Le500 ?? 0,
                                Le200 = viewModel.Le200 ?? 0,
                                Le100 = viewModel.Le100 ?? 0,
                                Le50 = viewModel.Le50 ?? 0,
                                Le20 = viewModel.Le20 ?? 0,
                                Le10 = viewModel.Le10 ?? 0,
                                Le5 = viewModel.Le5 ?? 0,
                                // المبلغ الذي دخل/خرج من الدرج هو السعر وليس الرصيد
                                TtlAmntTrnsAct = viewModel.TotalPriceTrx,
                                //TtlAmntTrnsAct = viewModel.BlncAmntOrderTrx,

                                // الرصيد بعد (هام جداً)
                                //TtlAmntAfter = afterTotal,
                                // المعادلة: الرصيد القديم + (التغيير بناءً على السعر)
                                TtlAmntAfter = ownTotal + cashOrDebtChange,
                                After500 = after500,
                                After200 = after200,
                                After100 = after100,
                                After50 = after50,
                                After20 = after20,
                                After10 = after10,
                                After5 = after5
                            };
                            await _context.TblDrwerTransActions.AddAsync(drawerTrx);

                            // تحديث رصيد الدرج الرئيسي (هام جداً أن يتم التحديث هنا)
                            userDrawer.TtlAmntOwned = afterTotal;
                            userDrawer.Le500 = after500;
                            userDrawer.Le200 = after200;
                            userDrawer.Le100 = after100;
                            userDrawer.Le50 = after50;
                            userDrawer.Le20 = after20;
                            userDrawer.Le10 = after10;
                            userDrawer.Le5 = after5;

                            _context.TblDrawers.Update(userDrawer);

                            //UserAccount.Indebtedness = (UserAccount.Indebtedness ?? 0) + sign * viewModel.BlncAmntOrderTrx;
                            // عند الحفظ في TblAccounts (حساب التاجر)
                            UserAccount.Indebtedness = (UserAccount.Indebtedness ?? 0) + cashOrDebtChange;
                            _context.TblAccounts.Update(UserAccount);

                        }
                        else
                        {
                            // === معالجة الآجل (حسابات التجار) ===
                            var receiverAccount = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == receiverId);
                            if (receiverAccount != null)
                            {
                                var debtBefore = receiverAccount.Indebtedness ?? 0;
                                var debtAfter = debtBefore + totalAmountForDebt; // الدين يزيد بقيمة العملية

                                receiverAccount.Indebtedness = debtAfter;
                                _context.TblAccounts.Update(receiverAccount);

                                // تسجيل حركة الدين
                                var senderAccount = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == senderID);
                                var debtTrx = new TblDebtTrnsAct
                                {
                                    ReferenceCode = refCode,
                                    AccidColToFk = senderAccount?.Id,
                                    AccidColFromFk = receiverAccount.Id,
                                    BlncTrnsAct = totalAmountForDebt,
                                    ColFrmDbtBefr = debtBefore,
                                    ColfrmDebtAfter = debtAfter,
                                    TransDate = today,
                                    DateAndTime = now,
                                    UserFk = senderID,
                                    ProvidFk = product.ServProvFk,
                                    TransTypeId = (prodType == 1) ? 1 : 2, // 1: بيع آجل، 2: شراء آجل (سداد)
                                    Description = (prodType == 1) ? "بيع رصيد (آجل)" : "شراء رصيد (آجل)"
                                };
                                await _context.TblDebtTrnsActs.AddAsync(debtTrx);
                            }
                        }

                        // ثالثاً: تسجيل العملية الرئيسية TblServTrnx
                        var servTrnx = new TblServTrnx
                        {
                            ReferenceCode = refCode,
                            SenderUserId = senderID,
                            RecvrUserId = receiverId,
                            SendrRegId = senderProvReg.Id,
                            //BlncAmntOrderTrx = cashOrDebtChange, // نسجل القيمة (+/-) لمعرفة اتجاه الكاش
                            BlncAmntOrderTrx = viewModel.BlncAmntOrderTrx, // نسجل قيمة الرصيد هنا
                            SendrBlncBfr = (int)(senderProvReg.Balance - providerChange), // الرصيد قبل
                            SendrBlncAftr = (int)senderProvReg.Balance, // الرصيد بعد
                            TransDate = today,
                            DateAndTime = now,
                            ServProdFk = viewModel.ServProdFk,
                            CustomerNoTrx = viewModel.CustomerNoTrx,
                            //SellPriceTrx = viewModel.SellPriceTrx,
                            SellPriceTrx = viewModel.TotalPriceTrx,        // سعر البيع هو نفسه السعر الكلي
                            //TotalPriceTrx = viewModel.TotalPriceTrx,
                            TotalPriceTrx = viewModel.TotalPriceTrx,       // نسجل السعر هنا
                            StatusFk = 1,
                            Description = isCashSale ? "عملية نقدية" : "عملية آجلة"
                        };
                        await _context.TblServTrnxes.AddAsync(servTrnx);

                        // الحفظ النهائي واعتماد التغييرات
                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();

                        TempData["SuccessMessage"] = "تمت العملية بنجاح";
                        return RedirectToAction("Index", "Home");
                    }
                    catch (Exception ex)
                    {
                        await dbTransaction.RollbackAsync();
                        var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                        ModelState.AddModelError("", "حدث خطأ: " + msg);
                    }
                }
            }

            // إعادة تحميل البيانات للـ View في حالة الخطأ
            ViewBag.ProviderName = product?.ServProvFkNavigation.ProviderName;
            ViewBag.ProductName = product?.ServProdName;
            ViewBag.ProviderLogoUrl = product?.ServProvFkNavigation.ProviderLogo;

            if (isCashSale)
            {
                var u = await _context.TblUsers.FindAsync(currentUserId);
                ViewBag.ShopName = u?.UserCode;
            }
            else
            {
                var u = await _context.TblUsers.FindAsync(viewModel.MerchantId);
                ViewBag.ShopName = u?.FullName;
            }

            return View(viewModel);
            //var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            //if (currentUserType > 7) return Forbid();
            //if (viewModel.SenderUserId != currentUserId) return BadRequest("تلاعب في بيانات المستخدم.");

            //// 1. التحقق من المنتج
            //// نستبدل FindAsync بـ Include + FirstOrDefaultAsync لجلب بيانات المزود
            //var product = await _context.TblServProducts
            //                            .Include(p => p.ServProvFkNavigation) // <-- هذا السطر هو الحل
            //                            .FirstOrDefaultAsync(p => p.Id == viewModel.ServProdFk); if (product == null) ModelState.AddModelError("", "المنتج غير موجود.");

            //// 2. التحقق من رصيد الوكيل لدى المزود
            //var senderProvReg = await _context.TblServProvRegs
            //    .FirstOrDefaultAsync(r => r.UserFk == currentUserId && r.ServProvFk == product.ServProvFk);

            //if (senderProvReg == null) ModelState.AddModelError("", "ليس لديك حساب لهذا المزوّد.");

            //// القاعدة 1: الإيداع (Type 1) يخصم من رصيد المزود -> يجب توفر رصيد
            //if ((product.TypeFk != 0 ? product.TypeFk : 1) == 1 && (senderProvReg?.Balance ?? 0) < viewModel.BlncAmntOrderTrx)
            //{
            //    ModelState.AddModelError("BlncAmntOrderTrx", "رصيدك لدى المزوّد لا يكفي لإتمام عملية الإيداع.");
            //}

            //// 3. التحقق من منطق الكاش والدرج
            //bool isCashSale = (viewModel.MerchantId == 0);
            //TblDrawer userDrawer = null;

            //if (isCashSale && ModelState.IsValid)
            //{
            //    userDrawer = await _context.TblDrawers.FirstOrDefaultAsync(d => d.UserFk == currentUserId);

            //    // إنشاء درج تلقائي إذا لم يوجد (لضمان سير العمل)
            //    if (userDrawer == null)
            //    {
            //        userDrawer = new TblDrawer
            //        {
            //            UserFk = currentUserId,
            //            Code = "Drw-" + currentUserId,
            //            IsActive = true,
            //            StartDate = DateOnly.FromDateTime(DateTime.Now),
            //            // تهيئة القيم بصفر
            //            Le500 = 0,
            //            Le200 = 0,
            //            Le100 = 0,
            //            Le50 = 0,
            //            Le20 = 0,
            //            Le10 = 0,
            //            Le5 = 0,
            //            TtlAmntOwned = 0
            //        };
            //        _context.TblDrawers.Add(userDrawer);
            //        await _context.SaveChangesAsync();
            //    }

            //    // حساب إجمالي النقدية المدخلة من الفيو موديل
            //    long totalCashInput = (viewModel.Le500 ?? 0) * 500 +
            //                          (viewModel.Le200 ?? 0) * 200 + (viewModel.Le100 ?? 0) * 100 +
            //                          (viewModel.Le50 ?? 0) * 50 + (viewModel.Le20 ?? 0) * 20 +
            //                          (viewModel.Le10 ?? 0) * 10 + (viewModel.Le5 ?? 0) * 5;

            //    // يجب أن يتطابق تفقيط النقدية مع مبلغ العملية
            //    if (totalCashInput != viewModel.BlncAmntOrderTrx)
            //    {
            //        ModelState.AddModelError("TotlMoneyTranact", $"إجمالي تفقيط النقدية ({totalCashInput}) لا يساوي المبلغ المطلوب ({viewModel.BlncAmntOrderTrx}).");
            //    }

            //    // القاعدة 2: السحب (Type 2) يخرج كاش من الدرج -> يجب توفر فئات نقدية
            //    if ((product.TypeFk != 0 ? product.TypeFk : 1) == 2)
            //    {
            //        if ((userDrawer.Le500 ?? 0) < (viewModel.Le500 ?? 0) ||
            //            (userDrawer.Le200 ?? 0) < (viewModel.Le200 ?? 0) ||
            //            (userDrawer.Le100 ?? 0) < (viewModel.Le100 ?? 0) ||
            //            (userDrawer.Le50 ?? 0) < (viewModel.Le50 ?? 0) ||
            //            (userDrawer.Le20 ?? 0) < (viewModel.Le20 ?? 0) ||
            //            (userDrawer.Le10 ?? 0) < (viewModel.Le10 ?? 0) ||
            //            (userDrawer.Le5 ?? 0) < (viewModel.Le5 ?? 0))
            //        {
            //            ModelState.AddModelError("", "لا يوجد نقدية كافية من الفئات المحددة في الدرج لإتمام عملية السحب.");
            //        }
            //    }
            //}

            //if (ModelState.IsValid)
            //{
            //    using (var dbTransaction = await _context.Database.BeginTransactionAsync())
            //    {
            //        try
            //        {
            //            int senderID = currentUserId;
            //            int receiverId = isCashSale ? currentUserId : viewModel.MerchantId;
            //            // Example fix in the Create POST action:
            //            int prodType = product.TypeFk != 0 ? product.TypeFk : 1;

            //            // تحديد المعاملات بناء على النوع
            //            // Type 1 (إيداع): يقل رصيد المزود (-)، تزيد المديونية/الكاش (+)
            //            // Type 2 (سحب): يزيد رصيد المزود (+)، تقل المديونية/الكاش (-)

            //            int providerChange = (prodType == 1) ? -viewModel.BlncAmntOrderTrx : viewModel.BlncAmntOrderTrx;
            //            int debtOrCashChange = (prodType == 1) ? viewModel.BlncAmntOrderTrx : -viewModel.BlncAmntOrderTrx;

            //            // حساب الإجمالي للعمليات (السعر + الرسوم إن وجدت)
            //            // في الكاش: المبلغ هو المبلغ. في الآجل: المديونية تتأثر بالسعر الكلي
            //            int debtAmountChange = (prodType == 1) ? viewModel.TotalPriceTrx : -viewModel.TotalPriceTrx;

            //            var refCode = DateTime.UtcNow.AddHours(2).ToString("yyMMddHHmmssff");
            //            var now = DateTime.UtcNow.AddHours(2);
            //            var today = DateOnly.FromDateTime(now);

            //            // 1. تحديث رصيد الوكيل لدى المزوّد (TblServProvRegs)
            //            senderProvReg.Balance += providerChange;
            //            _context.TblServProvRegs.Update(senderProvReg);

            //            // 2. معالجة الكاش (TblDrawer & TblDrwerTransAction)
            //            if (isCashSale && userDrawer != null)
            //            {
            //                // sign: 1 للإيداع (زيادة الدرج)، -1 للسحب (نقص الدرج)
            //                int sign = (prodType == 1) ? 1 : -1;

            //                // القيم الحالية (Own)
            //                int own500 = userDrawer.Le500 ?? 0;
            //                int own200 = userDrawer.Le200 ?? 0; int own100 = userDrawer.Le100 ?? 0;
            //                int own50 = userDrawer.Le50 ?? 0; int own20 = userDrawer.Le20 ?? 0;
            //                int own10 = userDrawer.Le10 ?? 0; int own5 = userDrawer.Le5 ?? 0;
            //                int ownTotal = userDrawer.TtlAmntOwned ?? 0;

            //                // القيم الجديدة (After)
            //                int after500 = own500 + (sign * (viewModel.Le500 ?? 0));
            //                int after200 = own200 + (sign * (viewModel.Le200 ?? 0));
            //                int after100 = own100 + (sign * (viewModel.Le100 ?? 0));
            //                int after50 = own50 + (sign * (viewModel.Le50 ?? 0));
            //                int after20 = own20 + (sign * (viewModel.Le20 ?? 0));
            //                int after10 = own10 + (sign * (viewModel.Le10 ?? 0));
            //                int after5 = own5 + (sign * (viewModel.Le5 ?? 0));
            //                int afterTotal = ownTotal + debtOrCashChange;

            //                // تسجيل حركة الدرج
            //                var drawerTrx = new TblDrwerTransAction
            //                {
            //                    RefCode = refCode,
            //                    DrawerIdFk = userDrawer.Id,
            //                    TransTypeId = (prodType == 1) ? 1 : 2, // 1: وارد نقدية، 2: منصرف نقدية
            //                    TransDate = today,
            //                    DateAndTime = now,
            //                    Description = (prodType == 1) ? $" تحصيل نقدية (إيداع كاش) - {product.ServProdName}" : $"صرف نقدية (سحب كاش ) - {product.ServProdName}",

            //                    // بيانات ما قبل
            //                    Own500 = own500,
            //                    Own200 = own200,
            //                    Own100 = own100,
            //                    Own50 = own50,
            //                    Own20 = own20,
            //                    Own10 = own10,
            //                    Own5 = own5,
            //                    TtlAmntOwned = ownTotal,

            //                    // بيانات الحركة
            //                    Le500 = viewModel.Le500 ?? 0,
            //                    Le200 = viewModel.Le200 ?? 0,
            //                    Le100 = viewModel.Le100 ?? 0,
            //                    Le50 = viewModel.Le50 ?? 0,
            //                    Le20 = viewModel.Le20 ?? 0,
            //                    Le10 = viewModel.Le10 ?? 0,
            //                    Le5 = viewModel.Le5 ?? 0,
            //                    TtlAmntTrnsAct = viewModel.BlncAmntOrderTrx,

            //                    // بيانات ما بعد
            //                    After500 = after500,
            //                    After200 = after200,
            //                    After100 = after100,
            //                    After50 = after50,
            //                    After20 = after20,
            //                    After10 = after10,
            //                    After5 = after5,
            //                    TtlAmntAfter = afterTotal
            //                };
            //                await _context.TblDrwerTransActions.AddAsync(drawerTrx);

            //                // تحديث رصيد الدرج
            //                userDrawer.Le500 = after500;
            //                userDrawer.Le200 = after200; userDrawer.Le100 = after100; userDrawer.Le50 = after50;
            //                userDrawer.Le20 = after20; userDrawer.Le10 = after10; userDrawer.Le5 = after5;
            //                userDrawer.TtlAmntOwned = afterTotal;
            //                _context.TblDrawers.Update(userDrawer);
            //            }

            //            // 3. معالجة الآجل (TblAccounts & TblDebtTrnsAct)
            //            if (!isCashSale)
            //            {
            //                var receiverAccount = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == receiverId);
            //                if (receiverAccount != null)
            //                {
            //                    var debtBefore = receiverAccount.Indebtedness ?? 0;
            //                    var debtAfter = debtBefore + debtAmountChange; // (+ للإيداع، - للسحب)

            //                    receiverAccount.Indebtedness = (int)debtAfter;
            //                    _context.TblAccounts.Update(receiverAccount);

            //                    // تسجيل حركة الدين
            //                    var senderAccount = await _context.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == senderID);
            //                    var debtTrx = new TblDebtTrnsAct
            //                    {
            //                        ReferenceCode = refCode,
            //                        AccidColToFk = senderAccount?.Id,
            //                        AccidColFromFk = receiverAccount.Id,
            //                        // رصيد العملية (موجب أو سالب)
            //                        BlncTrnsAct = debtOrCashChange,
            //                        ColFrmDbtBefr = debtBefore,
            //                        ColfrmDebtAfter = debtAfter,
            //                        TransDate = today,
            //                        DateAndTime = now,
            //                        UserFk = senderID,
            //                        ProvidFk = product.ServProvFk,
            //                        // نوع الحركة للدين (يمكن تخصيص أرقام مختلفة للإيداع والسحب)
            //                        TransTypeId = (prodType == 1) ? 1 : 2,
            //                        Description = (prodType == 1) ? "إيداع كاش" : "سحب كاش"
            //                    };
            //                    await _context.TblDebtTrnsActs.AddAsync(debtTrx);
            //                }
            //            }

            //            // 4. تسجيل المعاملة الرئيسية (TblServTrnx)
            //            var servTrnx = new TblServTrnx
            //            {
            //                ReferenceCode = refCode,
            //                SenderUserId = senderID,
            //                RecvrUserId = receiverId,
            //                SendrRegId = senderProvReg.Id,
            //                BlncAmntOrderTrx = debtOrCashChange, // المبلغ بالقيمة الموجبة أو السالبة
            //                                                     // أرصدة المزود (هامة جداً للتدقيق)
            //                SendrBlncBfr = (int)(senderProvReg.Balance - providerChange), // الرصيد قبل = الحالي - التغيير
            //                SendrBlncAftr = (int)senderProvReg.Balance,
            //                TransDate = today,
            //                DateAndTime = now,
            //                ServProdFk = viewModel.ServProdFk,
            //                CustomerNoTrx = viewModel.CustomerNoTrx,
            //                SellPriceTrx = viewModel.SellPriceTrx,
            //                TotalPriceTrx = viewModel.TotalPriceTrx,
            //                StatusFk = 1,
            //                Description = (prodType == 1) ? "إيداع كاش" : "سحب كاش"
            //            };
            //            await _context.TblServTrnxes.AddAsync(servTrnx);

            //            await _context.SaveChangesAsync();
            //            await dbTransaction.CommitAsync();

            //            TempData["SuccessMessage"] = "تمت العملية بنجاح";
            //            return RedirectToAction("Index", "Home");
            //        }
            //        catch (Exception ex)
            //        {
            //            await dbTransaction.RollbackAsync();

            //            // هذا التعديل سيظهر لك سبب الخطأ الحقيقي (مثلاً: FK conflict, Overflow, etc)
            //            var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            //            ModelState.AddModelError("", "تفاصيل الخطأ: " + errorMessage);
            //        }
            //    }
            //}

            //// إعادة تحميل البيانات للعرض في حالة الخطأ
            //ViewBag.ProviderName = product?.ServProvFkNavigation.ProviderName;
            //ViewBag.ProductName = product?.ServProdName;
            //ViewBag.ProviderLogoUrl = product?.ServProvFkNavigation.ProviderLogo;

            //// إعادة جلب اسم المحل للعرض
            //if (isCashSale)
            //{
            //    var u = await _context.TblUsers.FindAsync(currentUserId);
            //    ViewBag.ShopName = u?.UserCode;
            //}
            //else
            //{
            //    var u = await _context.TblUsers.FindAsync(viewModel.MerchantId);
            //    ViewBag.ShopName = u?.FullName;
            //}

            //return View(viewModel);
        }

        //// GET: TblServTrnxes/Create
        //// عدّلنا الـ Route ليقبل productId
        //[HttpGet("TblServTrnxes/Create/{productId}")]
        //public async Task<IActionResult> Create(int productId, int? prodRegID, int? merchantId)
        //{
        //    var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

        //    if (currentUserType > 7)
        //    {
        //        return Forbid();
        //    }

        //    // 1. جلب بيانات المنتج والمزوّد
        //    var product = await _context.TblServProducts
        //                                .Include(p => p.ServProvFkNavigation) // لجلب بيانات المزوّد
        //                                .FirstOrDefaultAsync(p => p.Id == productId);

        //    if (product == null)
        //    {
        //        return NotFound("المنتج غير موجود.");
        //    }

        //    // 2. جلب بيانات المستخدم الحالي (المرسل)
        //    var senderUser = await _context.TblServProvRegs
        //        .Include(r => r.UserFkNavigation) // <-- الإضافة المهمة هنا
        //        .FirstOrDefaultAsync(u => u.UserFk == currentUserId);
        //    if (senderUser == null)
        //    {
        //        return Unauthorized("المستخدم غير موجود.");
        //    }

        //    TblAccount targetAccount; // حساب الهدف (إما التاجر أو المستخدم نفسه)
        //    int targetUserId;         // معرّف الهدف

        //    if (merchantId.HasValue)
        //    {
        //        // === سيناريو التاجر ===
        //        targetUserId = merchantId.Value;
        //        targetAccount = await _context.TblAccounts
        //                                      .Include(a => a.UserIdFkNavigation)
        //                                      .FirstOrDefaultAsync(a => a.UserIdFk == merchantId.Value);

        //        if (targetAccount == null)
        //        {
        //            return NotFound("حساب التاجر غير موجود.");
        //        }
        //    }
        //    else
        //    {
        //        // === سيناريو البيع المباشر ===
        //        // الهدف هو المستخدم الحالي نفسه
        //        targetUserId = currentUserId;
        //        targetAccount = await _context.TblAccounts
        //                                      .Include(a => a.UserIdFkNavigation)
        //                                      .FirstOrDefaultAsync(a => a.UserIdFk == currentUserId);

        //        if (targetAccount == null)
        //        {
        //            return NotFound("حساب المستخدم الحالي غير موجود.");
        //        }
        //    }

        //    // 3. إنشاء وتعبئة الـ ViewModel
        //    var viewModel = new ServTrnxCreateViewModel
        //    {
        //        ServProdFk = productId,
        //        SenderUserId = currentUserId,// المستخدم الحالي هو الوكيل
        //        MerchantId = targetUserId, // <-- نستخدم معرّف الهدف                SendrBlncBfr = senderUser.Balance ?? 0, // افترضت أن الرصيد موجود في TblUsers
        //        IndebtBefore = targetAccount.Indebtedness ?? 0,
        //        BlncAmntOrderTrx = 0,
        //        ProductType = product.TypeFk != 0 ? product.TypeFk : 1, // مهم جداً للحسابات (1=شحن/خصم, 2=تحصيل/إضافة)
        //        SellPriceTrx = (int)Math.Ceiling(product.ServProdSellPrice ?? 0)
        //    };

        //    // 4. استخدام ViewBag لتمرير بيانات العرض الإضافية (مثل مشروعك القديم)
        //    ViewData["Title"] = "تنفيذ عملية شحن";
        //    ViewBag.ProviderName = product.ServProvFkNavigation.ProviderName;
        //    ViewBag.ProductName = product.ServProdName;
        //    ViewBag.ProviderLogoUrl = product.ServProvFkNavigation.ProviderLogo; // افترضت وجود حقل للشعار
        //    ViewBag.ShopName = senderUser.UserFkNavigation.FullName; // افترضت وجود اسم للمحل

        //    return View(viewModel);
        //}

        //// POST: TblServTrnxes/Create
        //// To protect from overposting attacks, enable the specific properties you want to bind to.
        //// For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create([Bind("Id,ReferenceCode,SenderUserId,SendrRegId,RecvrUserId,BlncAmntOrderTrx,SendrBlncBfr,SendrBlncAftr,TransDate,DateAndTime,OrderNo,ServProdFk,CustomerNoTrx,SellPriceTrx,FeesTrx,TotalPriceTrx,CnfrmNumOrPic,StatusFk,Description")] TblServTrnx tblServTrnx, ServTrnxCreateViewModel viewModel)
        //{
        //    var (currentUserId, currentUserType, currentUserCompanyId, currentUserBranchId) = GetCurrentUserInfo();

        //    if (currentUserType > 7)
        //    {
        //        // لا يوجد صلاحية لهذا المستخدم
        //        return Forbid(); // أو RedirectToAction لصفحة مخصصة
        //    }

        //    // تأكد أن المستخدم الذي يرسل الطلب هو نفس المستخدم المسجل دخوله
        //    if (viewModel.SenderUserId != currentUserId)
        //    {
        //        return BadRequest("Invalid user data.");
        //    }

        //    // جلب المستخدم (المرسل) للتأكد من وجود رصيد كافٍ وتحديثه
        //    var senderUser = await _context.TblServProvRegs.FindAsync(currentUserId);
        //    if (senderUser == null)
        //    {
        //        ModelState.AddModelError("", "Sender user not found.");
        //    }

        //    // يمكنك إضافة المزيد من التحققات هنا، مثل التحقق من الرصيد
        //    if (senderUser != null && (senderUser.Balance ?? 0) < viewModel.BlncAmntOrderTrx)
        //    {
        //        ModelState.AddModelError("BlncAmntOrderTrx", "الرصيد غير كافٍ لإتمام العملية.");
        //    }

        //    if (ModelState.IsValid)
        //    {
        //        int senderID = currentUserId;
        //        int receiverId = (viewModel.UserID > 0) ? viewModel.UserID : viewModel.Id;

        //        viewModel.ReferenceCode = DateTime.UtcNow.AddHours(2).ToString("yyMMddHHmmssff");
        //                     viewModel.TransDate = DateOnly.FromDateTime(DateTime.Today);
        //        viewModel.DateAndTime = Convert.ToDateTime(DateTime.UtcNow.AddHours(2).ToString(format: "F"));
        //        //serviceTrans.DateAndTime = Convert.ToDateTime(Session["UserTime"]);
        //        //serviceTrans.TransDate = Convert.ToDateTime(Session["UserDate"]);
        //        //int ReceivererRegID = (serviceTrans.ReceiverRegID > 0) ? serviceTrans.ReceiverRegID : 1;/////////////
        //        int senderRegId = (viewModel.SenderRegId > 0) ? viewModel.SenderRegId : 1;

        //        TblServProduct servProduct = _context.TblServProducts.Where(p => p.Id == viewModel.ServProdId).SingleOrDefault();
        //        int provider = (int)servProduct.ServProvFk;
        //        int prodType = (int)servProduct.TypeFk;

        //        int blncTrnsact = (prodType != 2) ? viewModel.BlncAmntOrderTrx : viewModel.BlncAmntOrderTrx * -1;
        //        int totalPrice = (prodType != 2) ? viewModel.TotalPriceTrx : viewModel.TotalPriceTrx * -1;

        //        // Update Receiver Balance///// Not available

        //        //Update Receiver Account
        //        TblAccount receiverAcc = _context.TblAccounts.Where(u => u.UserIdFk == receiverId).FirstOrDefault();
        //        var receiverDebtBefore = receiverAcc.Indebtedness;
        //        var receiverDebtAfter = receiverAcc.Indebtedness + totalPrice;
        //        receiverAcc.Indebtedness = receiverDebtAfter;

        //        // New Receiver Balance // Not Available

        //        // Update Sender Balance
        //        TblServProvReg senderReg = _context.TblServProvRegs.Where(s => s.UserFk == senderID
        //        && s.ServProvFk == provider).FirstOrDefault(); ////// to know provider
        //        var sendrBlncBfor = senderReg.Balance;
        //        senderReg.Balance -= blncTrnsact;
        //        var sendrBlncAftr = senderReg.Balance;

        //        // Update Sender Indebtness
        //        TblAccount senderAcc = _context.TblAccounts.Where(u => u.UserIdFk == senderID).FirstOrDefault();

        //        //Add New Sender Debt
        //        TblDebtTrnsAct BlncDebtTrnsAct = new TblDebtTrnsAct()
        //        {
        //            ReferenceCode = viewModel.ReferenceCode,
        //            AccidColToFk = senderAcc.Id,
        //            AccidColFromFk = receiverAcc.Id,
        //            AmntTrnsAct = 0,/////////// important
        //            BlncTrnsAct = blncTrnsact,///////////// important
        //            ColToDbtBfr = 0,
        //            ColToDbtAftr = 0,
        //            ColFrmDbtBefr = receiverDebtBefore,
        //            ColfrmDebtAfter = receiverDebtAfter,
        //            TransDate = viewModel.TransDate,
        //            DateAndTime = viewModel.DateAndTime,
        //            UserFk = senderID,
        //            ProvidFk = provider,
        //            TransTypeId = (prodType != 2) ? 1 : 6,
        //        };

        //        TblServTrnx servTrnx = new TblServTrnx()
        //        {
        //            ReferenceCode = viewModel.ReferenceCode,
        //            SenderUserId = senderID,
        //            SendrRegId = senderRegId,
        //            RecvrUserId = receiverId,
        //            //RecvrRegID = ReceivererRegID,// sender.id - provider - reg
        //            BlncAmntOrderTrx = blncTrnsact,
        //            SendrBlncBfr = (int)sendrBlncBfor,
        //            SendrBlncAftr = (int)sendrBlncAftr,
        //            //RecvrBlncBfr = 0,////////////////////
        //            //RecvrBlncAftr = 0,//////////////////
        //            TransDate = viewModel.TransDate,
        //            DateAndTime = viewModel.DateAndTime,
        //            OrderNo = viewModel.OrderNo,
        //            ServProdFk = viewModel.ServProdId,
        //            CustomerNoTrx = viewModel.BillingNumTrx,
        //            SellPriceTrx = viewModel.SellPriceTrx,
        //            FeesTrx = viewModel.FeesTrx,
        //            //TaxTrx = viewModel.TaxTrx,
        //            //TotalFeesTrx = viewModel.TotalFeesTrx,
        //            TotalPriceTrx = totalPrice /*serviceTrans.TotalPriceTrx*/,
        //            CnfrmNumOrPic = (viewModel.ConfirmPic != null) ? viewModel.ConfirmPic : "0",
        //            //TransTypeID = 1,
        //            StatusFk = 1
        //        };

        //        var orderNo = viewModel.OrderNo;
        //        TblServOrder servOrder = servOrder = _context.TblServOrders.Where(o => o.OrderNo == orderNo).SingleOrDefault();

        //        if (servOrder != null)
        //        {
        //            servOrder.StatusFk = 2;
        //        }

        //        if (ModelState.IsValid)
        //        {
        //            _context.TblServTrnxes.Add(servTrnx);
        //            _context.TblDebtTrnsActs.Add(BlncDebtTrnsAct);
        //            _context.Entry(senderReg).State = EntityState.Modified;
        //            _context.Entry(receiverAcc).State = EntityState.Modified;
        //            //db.Entry(ReceiverReg).State = EntityState.Modified;
        //            if (servOrder != null)///////////////
        //            {
        //                _context.Entry(servOrder).State = EntityState.Modified;
        //            }
        //            //db.tblServTrnxes.Add(servTrnx);
        //            _context.SaveChanges();
        //            return RedirectToAction("Index", "Home");
        //        }

        //        ViewBag.ServProd_FK = new SelectList(_context.TblServProducts, "Id", "ServProdCode", viewModel.ServProdId);
        //        ViewBag.Status_FK = new SelectList(_context.TblStatuses, "Id", "StatusName", viewModel.StatusId);
        //        return View(viewModel);
        //    }

        //    ViewData["Title"] = "تنفيذ عملية سحب / إيداع";
        //    ViewData["RecvrUserId"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServTrnx.RecvrUserId);
        //    ViewData["SenderUserId"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServTrnx.SenderUserId);
        //    ViewData["SendrRegId"] = new SelectList(_context.TblServProvRegs, "Id", "ServNo", tblServTrnx.SendrRegId);
        //    ViewData["ServProdFk"] = new SelectList(_context.TblServProducts, "Id", "ServProdCode", tblServTrnx.ServProdFk);
        //    ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblServTrnx.StatusFk);
        //    return View(viewModel);
        //}

        // GET: TblServTrnxes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblServTrnx = await _context.TblServTrnxes.FindAsync(id);
            if (tblServTrnx == null)
            {
                return NotFound();
            }
            ViewData["RecvrUserId"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServTrnx.RecvrUserId);
            ViewData["SenderUserId"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServTrnx.SenderUserId);
            ViewData["SendrRegId"] = new SelectList(_context.TblServProvRegs, "Id", "ServNo", tblServTrnx.SendrRegId);
            ViewData["ServProdFk"] = new SelectList(_context.TblServProducts, "Id", "ServProdCode", tblServTrnx.ServProdFk);
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblServTrnx.StatusFk);
            return View(tblServTrnx);
        }

        // POST: TblServTrnxes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ReferenceCode,SenderUserId,SendrRegId,RecvrUserId,BlncAmntOrderTrx,SendrBlncBfr,SendrBlncAftr,TransDate,DateAndTime,OrderNo,ServProdFk,CustomerNoTrx,SellPriceTrx,FeesTrx,TotalPriceTrx,CnfrmNumOrPic,StatusFk,Description")] TblServTrnx tblServTrnx)
        {
            if (id != tblServTrnx.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblServTrnx);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblServTrnxExists(tblServTrnx.Id))
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
            ViewData["RecvrUserId"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServTrnx.RecvrUserId);
            ViewData["SenderUserId"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServTrnx.SenderUserId);
            ViewData["SendrRegId"] = new SelectList(_context.TblServProvRegs, "Id", "ServNo", tblServTrnx.SendrRegId);
            ViewData["ServProdFk"] = new SelectList(_context.TblServProducts, "Id", "ServProdCode", tblServTrnx.ServProdFk);
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblServTrnx.StatusFk);
            return View(tblServTrnx);
        }

        // GET: TblServTrnxes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblServTrnx = await _context.TblServTrnxes
                .Include(t => t.RecvrUser)
                .Include(t => t.SenderUser)
                .Include(t => t.SendrReg)
                .Include(t => t.ServProdFkNavigation)
                .Include(t => t.StatusFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblServTrnx == null)
            {
                return NotFound();
            }

            return View(tblServTrnx);
        }

        // POST: TblServTrnxes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblServTrnx = await _context.TblServTrnxes.FindAsync(id);
            if (tblServTrnx != null)
            {
                _context.TblServTrnxes.Remove(tblServTrnx);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblServTrnxExists(int id)
        {
            return _context.TblServTrnxes.Any(e => e.Id == id);
        }
    }
}
