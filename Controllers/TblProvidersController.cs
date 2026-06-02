using DatabaseAccess.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CollectAgent.Controllers
{
    public class TblProvidersController : Controller
    {
        private readonly CollectAgentDBContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // إضافة هذا السطر

        public TblProvidersController(CollectAgentDBContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment; // تعميد البيئة

        }

        // ✅ الدالة المساعدة لرفع الصورة (تستخدم في الإضافة والتعديل)
        private async Task<string> UploadImage(IFormFile logoFile)
        {
            if (logoFile == null || logoFile.Length == 0) return null;

            string folder = "Content/ProvidersLogos/";
            // تحديد المسار الفيزيائي الكامل
            string serverFolder = Path.Combine(_webHostEnvironment.WebRootPath, folder);

            if (!Directory.Exists(serverFolder))
                Directory.CreateDirectory(serverFolder);

            string fileName = Guid.NewGuid().ToString() + "_" + logoFile.FileName;
            string filePath = Path.Combine(serverFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await logoFile.CopyToAsync(fileStream);
            }

            return folder + fileName; // إرجاع المسار النسبي لحفظه في الداتابيز
        }

        // GET: TblProviders
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblProviders.Include(t => t.ProvTypeFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // 2. دالة جديدة لجلب البيانات عند التمرير
        [HttpGet]
        public async Task<IActionResult> GetProvidersData(int pageNumber = 1, string search = "")
        {
            int pageSize = 9; // عدد الكروت في كل دفعة

            var query = _context.TblProviders.Include(t => t.ProvTypeFkNavigation).AsQueryable();

            // ✅✅  فلترة المفعلين فقط ✅✅
            // هذا السطر يضمن جلب من لديهم IsActive = true فقط
            //query = query.Where(x => x.IsActive == true);

            // تطبيق البحث إذا وجد
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => x.ProviderName.Contains(search) || x.ProviderCode.Contains(search));
            }

            var data = await query
                .OrderBy(x => x.Id) // ترتيب حسب الأحدث
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // إذا لم توجد بيانات نرجع لا شيء
            if (!data.Any())
            {
                return NoContent();
            }

            // نرجع البيانات داخل Partial View
            return PartialView("_ProviderListPartial", data);
        }

        // GET: TblProviders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProvider = await _context.TblProviders
                .Include(t => t.ProvTypeFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProvider == null)
            {
                return NotFound();
            }

            return View(tblProvider);
        }

        // GET: TblProviders/Create
        public IActionResult Create()
        {
            ViewData["ProvTypeFk"] = new SelectList(_context.TblProvTypes, "Id", "ProvTypeName");
            return View();
        }

        // POST: TblProviders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ProviderName,ProviderLogo,ProvMainColor,ProviderNo,ProvTypeFk,ProviderCode,AllowRtrn,IsActive")] TblProvider tblProvider, IFormFile logoFile)
        {
            if (ModelState.IsValid)
            {
                // استدعاء الدالة المساعدة
                tblProvider.ProviderLogo = await UploadImage(logoFile);

                tblProvider.IsActive = true; // تعيينه نشط تلقائياً كما طلبت سابقاً

                _context.Add(tblProvider);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ProvTypeFk"] = new SelectList(_context.TblProvTypes, "Id", "ProvTypeName", tblProvider.ProvTypeFk);
            return View(tblProvider);
        }

        // GET: TblProviders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProvider = await _context.TblProviders.FindAsync(id);
            if (tblProvider == null)
            {
                return NotFound();
            }
            ViewData["ProvTypeFk"] = new SelectList(_context.TblProvTypes, "Id", "ProvTypeName", tblProvider.ProvTypeFk);
            return View(tblProvider);
        }

        // POST: TblProviders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProviderName,ProviderLogo,ProvMainColor,ProviderNo,ProvTypeFk,ProviderCode,AllowRtrn,IsActive")] TblProvider tblProvider, IFormFile? logoFile)
        {
            if (id != tblProvider.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // استدعاء نفس الدالة المساعدة
                    string newLogoPath = await UploadImage(logoFile);

                    if (newLogoPath != null)
                    {
                        tblProvider.ProviderLogo = newLogoPath;
                    }
                    // إذا كان نل، سيبقى tblProvider.ProviderLogo كما هو (القادم من الحقل المخفي في الفورم)

                    _context.Update(tblProvider);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblProviderExists(tblProvider.Id))
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
            ViewData["ProvTypeFk"] = new SelectList(_context.TblProvTypes, "Id", "ProvTypeName", tblProvider.ProvTypeFk);
            return View(tblProvider);
        }

        // GET: TblProviders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProvider = await _context.TblProviders
                .Include(t => t.ProvTypeFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProvider == null)
            {
                return NotFound();
            }

            return View(tblProvider);
        }

        // POST: TblProviders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblProvider = await _context.TblProviders.FindAsync(id);
            if (tblProvider != null)
            {
                _context.TblProviders.Remove(tblProvider);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblProviderExists(int id)
        {
            return _context.TblProviders.Any(e => e.Id == id);
        }
    }
}
