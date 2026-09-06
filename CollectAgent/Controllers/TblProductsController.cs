using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DatabaseAccess.Models;

namespace CollectAgent.Controllers
{
    public class TblProductsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblProductsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblProducts
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblProducts.Include(t => t.ProviderFkNavigation).Include(t => t.UserFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblProducts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProduct = await _context.TblProducts
                .Include(t => t.ProviderFkNavigation)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProduct == null)
            {
                return NotFound();
            }

            return View(tblProduct);
        }

        // GET: TblProducts/Create
        public IActionResult Create()
        {
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode");
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            return View();
        }

        // POST: TblProducts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserFk,ProductCode,ProductName,ProviderFk,BuyPrice,SellPrice,BuyComm,SellComm,Balance,IsActive")] TblProduct tblProduct)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblProduct);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblProduct.ProviderFk);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblProduct.UserFk);
            return View(tblProduct);
        }

        // GET: TblProducts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProduct = await _context.TblProducts.FindAsync(id);
            if (tblProduct == null)
            {
                return NotFound();
            }
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblProduct.ProviderFk);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblProduct.UserFk);
            return View(tblProduct);
        }

        // POST: TblProducts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserFk,ProductCode,ProductName,ProviderFk,BuyPrice,SellPrice,BuyComm,SellComm,Balance,IsActive")] TblProduct tblProduct)
        {
            if (id != tblProduct.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblProduct);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblProductExists(tblProduct.Id))
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
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblProduct.ProviderFk);
            ViewData["UserFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblProduct.UserFk);
            return View(tblProduct);
        }

        // GET: TblProducts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProduct = await _context.TblProducts
                .Include(t => t.ProviderFkNavigation)
                .Include(t => t.UserFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProduct == null)
            {
                return NotFound();
            }

            return View(tblProduct);
        }

        // POST: TblProducts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblProduct = await _context.TblProducts.FindAsync(id);
            if (tblProduct != null)
            {
                _context.TblProducts.Remove(tblProduct);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblProductExists(int id)
        {
            return _context.TblProducts.Any(e => e.Id == id);
        }
    }
}
