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
    public class TblProdTypesController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblProdTypesController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblProdTypes
        public async Task<IActionResult> Index()
        {
            return View(await _context.TblProdTypes.ToListAsync());
        }

        // GET: TblProdTypes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProdType = await _context.TblProdTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProdType == null)
            {
                return NotFound();
            }

            return View(tblProdType);
        }

        // GET: TblProdTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: TblProdTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ProdTypeName,ProdTypeCode,IsActive")] TblProdType tblProdType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblProdType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tblProdType);
        }

        // GET: TblProdTypes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProdType = await _context.TblProdTypes.FindAsync(id);
            if (tblProdType == null)
            {
                return NotFound();
            }
            return View(tblProdType);
        }

        // POST: TblProdTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProdTypeName,ProdTypeCode,IsActive")] TblProdType tblProdType)
        {
            if (id != tblProdType.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblProdType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblProdTypeExists(tblProdType.Id))
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
            return View(tblProdType);
        }

        // GET: TblProdTypes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProdType = await _context.TblProdTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProdType == null)
            {
                return NotFound();
            }

            return View(tblProdType);
        }

        // POST: TblProdTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblProdType = await _context.TblProdTypes.FindAsync(id);
            if (tblProdType != null)
            {
                _context.TblProdTypes.Remove(tblProdType);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblProdTypeExists(int id)
        {
            return _context.TblProdTypes.Any(e => e.Id == id);
        }
    }
}
