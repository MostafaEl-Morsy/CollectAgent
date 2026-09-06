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
    public class TblProdCatsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblProdCatsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblProdCats
        public async Task<IActionResult> Index()
        {
            return View(await _context.TblProdCats.ToListAsync());
        }

        // GET: TblProdCats/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProdCat = await _context.TblProdCats
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProdCat == null)
            {
                return NotFound();
            }

            return View(tblProdCat);
        }

        // GET: TblProdCats/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: TblProdCats/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ProdCatName,ProdCatCode,IsActive")] TblProdCat tblProdCat)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblProdCat);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tblProdCat);
        }

        // GET: TblProdCats/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProdCat = await _context.TblProdCats.FindAsync(id);
            if (tblProdCat == null)
            {
                return NotFound();
            }
            return View(tblProdCat);
        }

        // POST: TblProdCats/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProdCatName,ProdCatCode,IsActive")] TblProdCat tblProdCat)
        {
            if (id != tblProdCat.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblProdCat);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblProdCatExists(tblProdCat.Id))
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
            return View(tblProdCat);
        }

        // GET: TblProdCats/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProdCat = await _context.TblProdCats
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProdCat == null)
            {
                return NotFound();
            }

            return View(tblProdCat);
        }

        // POST: TblProdCats/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblProdCat = await _context.TblProdCats.FindAsync(id);
            if (tblProdCat != null)
            {
                _context.TblProdCats.Remove(tblProdCat);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblProdCatExists(int id)
        {
            return _context.TblProdCats.Any(e => e.Id == id);
        }
    }
}
