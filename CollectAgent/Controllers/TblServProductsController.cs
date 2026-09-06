using DatabaseAccess;
using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize] // <<< يحمي الـ Controller بالكامل، وهذا ممتاز!
    public class TblServProductsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblServProductsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // --- دالة مساعدة لجلب بيانات المستخدم الحالي ---
        private (int UserId, int UserType) GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirstValue(ClaimTypes.Role); // تم تعيينه كـ Role في AccountController

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userTypeClaim))
            {
                // هذا لا يجب أن يحدث لمستخدم مسجل دخوله، ولكنه احتياط جيد
                throw new InvalidOperationException("User claims are not available.");
            }

            return (int.Parse(userIdClaim), int.Parse(userTypeClaim));
        }

        // GET: TblServProducts
        public async Task<IActionResult> Index()
        {
            //var (currentUserId, currentUserType) = GetCurrentUserInfo();

            //if (currentUserType > 7)
            //{
            //    // لا يوجد صلاحية لهذا المستخدم
            //    return Forbid(); // أو RedirectToAction لصفحة مخصصة
            //}
            //var collectAgentDBContext = _context.TblServProducts.Include(t => t.CategoryFkNavigation).Include(t => t.ServProvFkNavigation).Include(t => t.TypeFkNavigation).Include(t => t.UserIdFkNavigation);
            //return View(await collectAgentDBContext.ToListAsync());
            return View();
        }

        // دالة جديدة لجلب البيانات عند التمرير
        [HttpGet]
        public async Task<IActionResult> GetProductsData(int pageNumber = 1, int pageSize = 20)
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                return Forbid();
            }

            var query = _context.TblServProducts
                .Include(t => t.CategoryFkNavigation)
                .Include(t => t.ServProvFkNavigation)
                .Include(t => t.TypeFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .AsNoTracking() // لتحسين الأداء
                .OrderByDescending(t => t.Id); // ترتيب البيانات (الأحدث أولاً مثلاً)

            // تطبيق المنطق الخاص بالصفحات
            var pagedData = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // إذا لم تعد هناك بيانات، نرجع محتوى فارغ
            if (!pagedData.Any())
            {
                return NoContent();
            }

            // إرجاع البيانات داخل الـ Partial View
            return PartialView("_ProductRow", pagedData);
        }

        // GET: TblServProducts/ServIndex
        [HttpGet]
        public ActionResult ServIndex(int id)
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            //HttpContext.Session.SetInt32("merchantID", id);

            // بدلاً من Session، نستخدم ViewData أو ViewBag
            ViewData["MerchantId"] = id;

            return View();
        }

        // GET: TblServProducts/Wallet
        public async Task<IActionResult> Wallet(int? provType, int? merchantId)
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            // نمرر merchantId (سواء كان له قيمة أو null) إلى الـ View
            ViewData["MerchantId"] = merchantId;

            //var userID = (int)Session["UserID"];
            ViewBag.CurrentUserID = currentUserId;

            // الكود السابق كان يعاني من مشكلة N+1 حيث يتم استدعاء قاعدة البيانات داخل حلقة تكرار
            // الكود الجديد يقوم بكل العمليات في استعلام واحد فقط، وهو أكثر كفاءة بمراحل

            // نبدأ بالاستعلام من جدول مزودي الخدمة
            var query = _context.TblProviders.AsQueryable();

            // إذا تم تحديد نوع مزود الخدمة، نقوم بفلترة النتائج
            if (provType.HasValue)
            {
                query = query.Where(p => p.ProvTypeFk == provType);
            }

            // الآن نقوم بفلترة مزودي الخدمة ليظهر فقط من سجل بهم المستخدم الحالي
            // هذا يتم عبر استعلام فرعي باستخدام Any وهو عالي الكفاءة
            var userProviders = await query
                .Where(p => _context.TblServProvRegs.Any(r => r.UserFk == currentUserId && r.ServProvFk == p.Id))
                .Distinct() // لضمان عدم تكرار البيانات
                .ToListAsync();

            return View(userProviders);

        }

        // GET: TblServProducts/CashBalance
        public async Task<IActionResult> CashBalance(int? senderID, int? provID, int? provType, int? merchantId)
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            // 1. بناء الاستعلام الأساسي لتجنب التكرار
            // نستهدف جدول TblServProvRegs ونقوم بتضمين TblProvider للاستفادة من بياناته
            var query = _context.TblServProvRegs
                                .Include(p => p.ServProvFkNavigation) // تضمين الجدول المرتبط لتحسين الأداء وتصحيح الخطأ
                                .Where(p => p.UserFk == currentUserId && p.IsActive == true);

            // 2. إضافة فلتر إضافي بشكل شرطي إذا كان provID موجوداً
            if (provID.HasValue && provID.Value > 0)
            {
                query = query.Where(p => p.ServProvFk == provID.Value);
            }

            // 3. إضافة الفلتر الأخير بناءً على نوع المزوّد
            // هنا تم تصحيح الخطأ باستخدام خاصية التنقل الصحيحة
            // لاحظ أننا نفلتر على الجدول المرتبط مباشرةً
            query = query.Where(p => p.ServProvFkNavigation.ProvTypeFk == provType);

            // 4. تنفيذ الاستعلام على قاعدة البيانات بشكل غير متزامن وتحويله إلى قائمة
            // هذا يحل مشكلة تمرير IQueryable ويستخدم async/await بشكل صحيح
            var walletsData = await query.ToListAsync();

            // إنشاء وتعبئة الـ ViewModel
            var viewModel = new CashBalanceViewModel
            {
                CurrentUserId = currentUserId,
                CurrentUserType = currentUserType,
                Wallets = walletsData,
                MerchantId = merchantId // <-- نمرر القيمة إلى الـ ViewModel
            };
            ////int userId = (int)Session["UserID"];
            //IQueryable<TblServProvReg> sumWallets = null; // Explicitly define the type to fix CS0815

            //if (provID > 0)
            //{
            //    sumWallets = _context.TblServProvRegs.Where(p => p.UserFk == currentUserId && p.ServProvFk == provID && p.ServProvFkNavigation.ProvTypeFk == provType && p.IsActive == true);
            //}
            //else
            //{
            //    sumWallets = _context.TblServProvRegs.Where(p => p.UserFk == currentUserId && p.ServProvFkNavigation.ProvTypeFk == provType && p.IsActive == true);
            //}
            return View(viewModel);
        }

        // GET: TblServProducts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            if (id == null)
            {
                return NotFound();
            }

            var tblServProduct = await _context.TblServProducts
                .Include(t => t.CategoryFkNavigation)
                .Include(t => t.ServProvFkNavigation)
                .Include(t => t.TypeFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblServProduct == null)
            {
                return NotFound();
            }

            return View(tblServProduct);
        }

        // GET: TblServProducts/Create
        public IActionResult Create()
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            ViewData["CategoryFk"] = new SelectList(_context.TblProdCats, "Id", "ProdCatName");
            ViewData["ServProvFk"] = new SelectList(_context.TblProviders, "Id", "ProviderName");
            ViewData["TypeFk"] = new SelectList(_context.TblProdTypes, "Id", "ProdTypeName");
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            return View();
        }

        // POST: TblServProducts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserIdFk,ServProdCode,ServProdName,ServProvFk,CategoryFk,TypeFk,ServProdBuyPrice,ServProdFees,ServProdSellPrice,ServProdBuyComm,ServProdSellComm,ServProdBalance,IsActive")] TblServProduct tblServProduct)
        {
            var (currentUserId, currentUserType) = GetCurrentUserInfo();

            if (currentUserType > 7)
            {
                // لا يوجد صلاحية لهذا المستخدم
                return Forbid(); // أو RedirectToAction لصفحة مخصصة
            }

            if (ModelState.IsValid)
            {
                _context.Add(tblServProduct);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryFk"] = new SelectList(_context.TblProdCats, "Id", "ProdCatName", tblServProduct.CategoryFk);
            ViewData["ServProvFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblServProduct.ServProvFk);
            ViewData["TypeFk"] = new SelectList(_context.TblProdTypes, "Id", "ProdTypeName", tblServProduct.TypeFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServProduct.UserIdFk);
            return View(tblServProduct);
        }

        // GET: TblServProducts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblServProduct = await _context.TblServProducts.FindAsync(id);
            if (tblServProduct == null)
            {
                return NotFound();
            }
            ViewData["CategoryFk"] = new SelectList(_context.TblProdCats, "Id", "ProdCatName", tblServProduct.CategoryFk);
            ViewData["ServProvFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblServProduct.ServProvFk);
            ViewData["TypeFk"] = new SelectList(_context.TblProdTypes, "Id", "ProdTypeName", tblServProduct.TypeFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServProduct.UserIdFk);
            return View(tblServProduct);
        }

        // POST: TblServProducts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserIdFk,ServProdCode,ServProdName,ServProvFk,CategoryFk,TypeFk,ServProdBuyPrice,ServProdFees,ServProdSellPrice,ServProdBuyComm,ServProdSellComm,ServProdBalance,IsActive")] TblServProduct tblServProduct)
        {
            if (id != tblServProduct.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblServProduct);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblServProductExists(tblServProduct.Id))
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
            ViewData["CategoryFk"] = new SelectList(_context.TblProdCats, "Id", "ProdCatName", tblServProduct.CategoryFk);
            ViewData["ServProvFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblServProduct.ServProvFk);
            ViewData["TypeFk"] = new SelectList(_context.TblProdTypes, "Id", "ProdTypeName", tblServProduct.TypeFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServProduct.UserIdFk);
            return View(tblServProduct);
        }

        // GET: TblServProducts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblServProduct = await _context.TblServProducts
                .Include(t => t.CategoryFkNavigation)
                .Include(t => t.ServProvFkNavigation)
                .Include(t => t.TypeFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblServProduct == null)
            {
                return NotFound();
            }

            return View(tblServProduct);
        }

        // POST: TblServProducts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblServProduct = await _context.TblServProducts.FindAsync(id);
            if (tblServProduct != null)
            {
                _context.TblServProducts.Remove(tblServProduct);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblServProductExists(int id)
        {
            return _context.TblServProducts.Any(e => e.Id == id);
        }
    }
}
