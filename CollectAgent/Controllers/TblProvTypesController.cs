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
    public class TblProvTypesController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblProvTypesController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblProvTypes
        public async Task<IActionResult> Index()
        {
            return View(await _context.TblProvTypes.ToListAsync());
        }

        // GET: TblProvTypes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProvType = await _context.TblProvTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProvType == null)
            {
                return NotFound();
            }

            return View(tblProvType);
        }

        // GET: TblProvTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: TblProvTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ProvTypeName,IsActive")] TblProvType tblProvType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblProvType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tblProvType);
        }

        // GET: TblProvTypes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProvType = await _context.TblProvTypes.FindAsync(id);
            if (tblProvType == null)
            {
                return NotFound();
            }
            return View(tblProvType);
        }

        // POST: TblProvTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProvTypeName,IsActive")] TblProvType tblProvType)
        {
            if (id != tblProvType.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblProvType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblProvTypeExists(tblProvType.Id))
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
            return View(tblProvType);
        }

        // GET: TblProvTypes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProvType = await _context.TblProvTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProvType == null)
            {
                return NotFound();
            }

            return View(tblProvType);
        }

        // POST: TblProvTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblProvType = await _context.TblProvTypes.FindAsync(id);
            if (tblProvType != null)
            {
                _context.TblProvTypes.Remove(tblProvType);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblProvTypeExists(int id)
        {
            return _context.TblProvTypes.Any(e => e.Id == id);
        }
    }
}
