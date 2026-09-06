using CollectAgent.Models;
using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Security.Claims;

namespace CollectAgent.Controllers
{
    [Authorize]
    public class CollController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public CollController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // --- دالة مساعدة لجلب بيانات المستخدم الحالي ---
        private (int UserId, int UserType, int? CompanyId, int? BranchId) GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirstValue(ClaimTypes.Role);
            var companyIdClaim = User.FindFirstValue("CompanyId");
            var branchIdClaim = User.FindFirstValue("BranchId");

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userTypeClaim))
            {
                throw new InvalidOperationException("بيـانـات المـستخـدم غـير مـوجـودة");
            }

            int.TryParse(userIdClaim, out int userId);
            int.TryParse(userTypeClaim, out int userType);
            int? companyId = int.TryParse(companyIdClaim, out int cId) ? cId : null;
            int? branchId = int.TryParse(branchIdClaim, out int bId) ? bId : null;

            return (userId, userType, companyId, branchId);
        }

        // --- دالة مساعدة لجلب البيانات المفلترة ---
        private async Task<List<TblAccount>> GetFilteredDebtorAccountsAsync()
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();

            var accountsQuery = _context.TblAccounts
                .Include(a => a.UserIdFkNavigation)
                .Where(i => i.Indebtedness != 0 && i.IsActive == true);

            if (currentUserType == 7) // المندوب
            {
                accountsQuery = accountsQuery.Where(i => i.UserIdFkNavigation.ParentUser == currentUserId);
            }
            else if (currentUserType == 6) // الوكيل
            {
                var representativeIds = await _context.TblUsers
                    .Where(u => u.ParentUser == currentUserId && u.UserTypeFk == 7)
                    .Select(u => u.Id)
                    .ToListAsync();

                var parentIdsToShow = new List<int> { currentUserId };
                parentIdsToShow.AddRange(representativeIds);

                accountsQuery = accountsQuery.Where(i => i.UserIdFkNavigation.ParentUser != null && parentIdsToShow.Contains(i.UserIdFkNavigation.ParentUser.Value));
            }
            // الإدارة (أقل من 6) ترى كل شيء

            return await accountsQuery.ToListAsync();
        }

        // GET: CollController/Index
        public async Task<IActionResult> Index()
        {
            var (_, currentUserType, _, _) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            var debtor = await GetFilteredDebtorAccountsAsync();
            return View(debtor);
        }

        // دالة التصدير للإكسل (المحسنة)
        public async Task<IActionResult> ExportToExcel()
        {
            var debtor = await GetFilteredDebtorAccountsAsync();
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("المديونيات");
                worksheet.View.RightToLeft = true;

                worksheet.Cells[1, 1].Value = "كود الحساب";
                worksheet.Cells[1, 2].Value = "اسم المتجر";
                worksheet.Cells[1, 3].Value = "المديونية الحالية";

                using (var range = worksheet.Cells[1, 1, 1, 3])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                var row = 2;
                foreach (var account in debtor)
                {
                    worksheet.Cells[row, 1].Value = account.Code;
                    worksheet.Cells[row, 2].Value = account.UserIdFkNavigation.FullName;
                    worksheet.Cells[row, 3].Value = account.Indebtedness;
                    row++;
                }

                if (debtor.Count > 0)
                {
                    worksheet.Cells[2, 3, row - 1, 3].Style.Numberformat.Format = "#,##0";
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var stream = new MemoryStream();
                await package.SaveAsAsync(stream);
                stream.Position = 0;

                string fileName = $"Debts_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        // GET: CollController/Create/5
        public async Task<IActionResult> Create(int id)
        {
            var (_, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            var accountToCollectFrom = await _context.TblAccounts
                .Include(a => a.UserIdFkNavigation)
                .FirstOrDefaultAsync(a => a.UserIdFk == id);

            if (accountToCollectFrom == null)
            {
                TempData["ErrorMessage"] = "لا يوجد حساب !!! !!!";
                return RedirectToAction(nameof(Index));
            }

            await RepopulateViewBagForCreate(id);

            var user = await _context.TblUsers.FindAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "لا يوجد حساب !!! !!!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ShopName = accountToCollectFrom.UserIdFkNavigation.UserCode;
            ViewBag.IsAmountFieldDisabled = (currentUserType >= 7);

            var viewModel = new CollectTransferAction
            {
                CollectFromID = id,
                CollectFromDebtBfor = (int)(accountToCollectFrom.Indebtedness ?? 0)
            };

            if (user.UserTypeFk < 8)
            {
                var userDrawer = await _context.TblDrawers.FirstOrDefaultAsync(u => u.UserFk == id);
                viewModel.Le500 = userDrawer?.Le500 ?? 0;
                viewModel.Le200 = userDrawer?.Le200 ?? 0;
                viewModel.Le100 = userDrawer?.Le100 ?? 0;
                viewModel.Le50 = userDrawer?.Le50 ?? 0;
                viewModel.Le20 = userDrawer?.Le20 ?? 0;
                viewModel.Le10 = userDrawer?.Le10 ?? 0;
                viewModel.Le5 = userDrawer?.Le5 ?? 0;
            }

            return View(viewModel);
        }

        // POST: CollController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CollectTransferAction collectTransferAction)
        {
            var (currentUserId, currentUserType, _, _) = GetCurrentUserInfo();
            if (currentUserType > 7)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لعرض البيانات !!! !!!";
                return RedirectToAction("Index", "Home");
            }

            int collectFromUserID = collectTransferAction.CollectFromID;

            if (collectTransferAction.BlncReturned > 0 && !collectTransferAction.SupplierRegistrationId.HasValue)
            {
                ModelState.AddModelError("SupplierRegistrationId", "يجب اختيار حساب المورد عند إرجاع الرصيد.");
            }

            if (!ModelState.IsValid)
            {
                await RepopulateViewBagForCreate(collectFromUserID);
                var account = await _context.TblAccounts
                    .Include(a => a.UserIdFkNavigation)
                    .FirstOrDefaultAsync(a => a.UserIdFk == collectFromUserID);
                ViewBag.ShopName = account?.UserIdFkNavigation?.UserCode ?? "غير معروف";
                return View(collectTransferAction);
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var dateAndTime = collectTransferAction.TransactionTimestamp;  // استخدام وقت الـ View
                    var transDate = DateOnly.FromDateTime(dateAndTime);
                    var referenceCode = dateAndTime.ToString("yyMMddHHmmssff");

                    var collectorAccount = await _context.TblAccounts.SingleAsync(a => a.UserIdFk == currentUserId);
                    var collectorDrawer = await _context.TblDrawers.SingleAsync(d => d.UserFk == currentUserId);
                    var debtorAccount = await _context.TblAccounts.SingleAsync(a => a.UserIdFk == collectFromUserID);
                    var debtorUser = await _context.TblUsers.SingleAsync(u => u.Id == collectFromUserID);

                    int cashCollected = collectTransferAction.TotlMoneyTranact;
                    int balanceReturned = collectTransferAction.BlncReturned > 0 ? collectTransferAction.BlncReturned : 0;
                    //int totalCollectedAmount = cashCollected;

                    int totalCollcted = cashCollected + balanceReturned;
                    if (totalCollcted <= 0) throw new InvalidOperationException("يجب أن يكون المبلغ المحصل أكبر من الصفر.");

                    var collectorDrawerBefore = collectorDrawer.TtlAmntOwned;

                    // تحديث درج المحصل
                    if (cashCollected > 0)
                    {

                        // تحديث الفئات
                        collectorDrawer.Le500 = (collectorDrawer.Le500 ?? 0) + (collectTransferAction.Le500 ?? 0);
                        collectorDrawer.Le200 = (collectorDrawer.Le200 ?? 0) + (collectTransferAction.Le200 ?? 0);
                        collectorDrawer.Le100 = (collectorDrawer.Le100 ?? 0) + (collectTransferAction.Le100 ?? 0);
                        collectorDrawer.Le50 = (collectorDrawer.Le50 ?? 0) + (collectTransferAction.Le50 ?? 0);
                        collectorDrawer.Le20 = (collectorDrawer.Le20 ?? 0) + (collectTransferAction.Le20 ?? 0);
                        collectorDrawer.Le10 = (collectorDrawer.Le10 ?? 0) + (collectTransferAction.Le10 ?? 0);
                        collectorDrawer.Le5 = (collectorDrawer.Le5 ?? 0) + (collectTransferAction.Le5 ?? 0);
                        collectorDrawer.TtlAmntOwned = (collectorDrawer.TtlAmntOwned ?? 0) + cashCollected; // تحديث الإجمالي

                        // إعادة حساب الإجمالي بدقة
                        collectorDrawer.TtlAmntOwned =
                            ((collectorDrawer.Le500 ?? 0) * 500) +
                            ((collectorDrawer.Le200 ?? 0) * 200) +
                            ((collectorDrawer.Le100 ?? 0) * 100) +
                            ((collectorDrawer.Le50 ?? 0) * 50) +
                            ((collectorDrawer.Le20 ?? 0) * 20) +
                            ((collectorDrawer.Le10 ?? 0) * 10) +
                            ((collectorDrawer.Le5 ?? 0) * 5);

                        _context.TblDrwerTransActions.Add(new TblDrwerTransAction
                        {
                            RefCode = referenceCode,
                            DrawerIdFk = collectorDrawer.Id,
                            TtlAmntOwned = collectorDrawerBefore,
                            TtlAmntTrnsAct = cashCollected,
                            TtlAmntAfter = collectorDrawer.TtlAmntOwned,
                            TransDate = transDate,
                            DateAndTime = dateAndTime,
                            TransTypeId = 2,
                            Description = "تحصيل نقدي"
                        });
                    }

                    int? providerIdForDebt = null; // لتخزين كود المورد لاستخدامه في حركة المديونية

                    // 2. تحديث الرصيد (استرجاع رصيد)
                    if (balanceReturned > 0)
                    {
                        var debtorReg = await _context.TblRegistrations.FindAsync(collectTransferAction.SupplierRegistrationId.Value);
                        if (debtorReg == null || debtorReg.UserFk != collectFromUserID)
                            throw new Exception("حساب المورد غير صحيح.");

                        // التحقق من الرصيد للجميع (مناديب وتجار) لمنع السالب
                        //if (debtorReg.Balance < balanceReturned)
                        //    throw new Exception($"الرصيد المتاح ({debtorReg.Balance}) غير كافٍ لاسترجاع ({balanceReturned}).");

                        // التحقق من رصيد المندوب لمنع السالب
                        if (debtorUser.UserTypeFk == 7 && debtorReg.Balance < balanceReturned)
                            throw new Exception("رصيد المندوب غير كافٍ.");

                        var collectorReg = await _context.TblRegistrations.FirstOrDefaultAsync(r => r.UserFk == currentUserId && r.ProviderFk == debtorReg.ProviderFk);
                        if (collectorReg == null)
                            throw new Exception("ليس لديك حساب لدى هذا المورد.");

                        providerIdForDebt = debtorReg.ProviderFk; // حفظنا المورد هنا

                        var debtorBalanceBefore = debtorReg.Balance;
                        var collectorBalanceBefore = collectorReg.Balance;

                        debtorReg.Balance -= balanceReturned;
                        collectorReg.Balance += balanceReturned;

                        _context.TblBlncTrnsActs.Add(new TblBlncTrnsAct
                        {
                            UserFk = currentUserId, // المستخدم القائم بالعملية

                            // --- تصحيح هام: ---
                            // ProdidFk = debtorReg.ProviderFk, <<-- هذا كان الخطأ، لا تضع كود المورد في كود المنتج
                            ProdidFk = null, // اتركه فارغاً إلا إذا كان لديك Id لمنتج "استرجاع رصيد" في جدول المنتجات

                            ReferenceCode = referenceCode,
                            SendrUserId = collectFromUserID,
                            RecvrUserId = currentUserId,
                            SendrRegId = debtorReg.Id,
                            RecvrRegId = collectorReg.Id,
                            SendrBlncBfr = debtorBalanceBefore,
                            BlncTrnsfr = balanceReturned,
                            SendrBlncAftr = debtorReg.Balance,
                            RecvrBlncBfr = collectorBalanceBefore,
                            RecvrBlncAftr = collectorReg.Balance,
                            TransDate = transDate,
                            DateAndTime = dateAndTime,
                            TransTypeId = 3,
                            Description = "تحصيل استرجاع رصيد"
                        });
                    }

                    // تحديث المديونيات
                    var collectorDebtBefore = collectorAccount.Indebtedness;
                    var debtorDebtBefore = debtorAccount.Indebtedness;

                    if (debtorUser.UserTypeFk > 7)
                    {
                        // مديونية التاجر تقل بمقدار التحصيل الكلي (نقدي + استرجاع رصيد)
                        debtorAccount.Indebtedness -= totalCollcted;
                    }
                    else
                    {
                        // مديونية العميل تقل (لأنه سدد نقدا)
                        debtorAccount.Indebtedness -= cashCollected;
                    }
                    // مديونية المحصل تقل (لأنه حصّل مبلغاً كان مديوناً به النظام له)
                    collectorAccount.Indebtedness += cashCollected; // تصحيح: تزيد (تقترب من الصفر)

                    _context.TblDebtTrnsActs.Add(new TblDebtTrnsAct
                    {
                        ReferenceCode = referenceCode,
                        AccidColToFk = collectorAccount.Id,
                        AccidColFromFk = debtorAccount.Id,
                        AmntTrnsAct = cashCollected,
                        BlncTrnsAct = balanceReturned,
                        ColToDbtBfr = collectorDrawerBefore, //collectorDebtBefore,
                        ColToDbtAftr = collectorDrawer.TtlAmntOwned, //collectorAccount.Indebtedness,
                        ColFrmDbtBefr = debtorDebtBefore,
                        ColfrmDebtAfter = debtorAccount.Indebtedness,
                        TransDate = transDate,
                        DateAndTime = dateAndTime,
                        UserFk = currentUserId,
                        ProvidFk = providerIdForDebt ?? 1, // نستخدم المورد الذي تم استرجاع الرصيد منه، أو null لو كاش فقط
                        TransTypeId = 2,
                        Description = "تحصيل نقدي / رصيد"
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    // --- إظهار الخطأ الداخلي ---
                    var realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    ModelState.AddModelError("", $"حدث خطأ: {realError}");

                    await RepopulateViewBagForCreate(collectFromUserID);
                    var account = await _context.TblAccounts.Include(a => a.UserIdFkNavigation).FirstOrDefaultAsync(a => a.UserIdFk == collectFromUserID);
                    ViewBag.ShopName = account?.UserIdFkNavigation?.FullName ?? "غير معروف";
                    return View(collectTransferAction);
                }
            }
        }

        // دالة مساعدة لتعبئة ViewBag
        private async Task RepopulateViewBagForCreate(int debtorUserId)
        {
            var supplierAccounts = await _context.TblRegistrations
                .Include(r => r.ProviderFkNavigation)
                .Where(r => r.UserFk == debtorUserId && r.IsActive == true)
                .Select(r => new
                {
                    Id = r.Id,
                    DisplayText = r.ProviderFkNavigation.ProviderName + " (رصيده: " + r.Balance + ")"
                })
                .ToListAsync();
            ViewBag.SupplierAccounts = new SelectList(supplierAccounts, "Id", "DisplayText");
        }

        public async Task<IActionResult> GetReceiverDebt(int? id)
        {
            if (id == null || id == 0) return Json(new { data = 0 });
            var receiver = await _context.TblAccounts.FirstOrDefaultAsync(r => r.UserIdFk == id);
            return Json(new { data = receiver?.Indebtedness ?? 0 });
        }
    }
}