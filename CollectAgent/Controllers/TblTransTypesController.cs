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
    public class TblTransTypesController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblTransTypesController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblTransTypes
        public async Task<IActionResult> Index()
        {
            return View(await _context.TblTransTypes.ToListAsync());
        }

        // GET: TblTransTypes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblTransType = await _context.TblTransTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblTransType == null)
            {
                return NotFound();
            }

            return View(tblTransType);
        }

        // GET: TblTransTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: TblTransTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,TransTypeName,IsActive")] TblTransType tblTransType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblTransType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tblTransType);
        }

        // GET: TblTransTypes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblTransType = await _context.TblTransTypes.FindAsync(id);
            if (tblTransType == null)
            {
                return NotFound();
            }
            return View(tblTransType);
        }

        // POST: TblTransTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TransTypeName,IsActive")] TblTransType tblTransType)
        {
            if (id != tblTransType.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblTransType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblTransTypeExists(tblTransType.Id))
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
            return View(tblTransType);
        }

        // GET: TblTransTypes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblTransType = await _context.TblTransTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblTransType == null)
            {
                return NotFound();
            }

            return View(tblTransType);
        }

        // POST: TblTransTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblTransType = await _context.TblTransTypes.FindAsync(id);
            if (tblTransType != null)
            {
                _context.TblTransTypes.Remove(tblTransType);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblTransTypeExists(int id)
        {
            return _context.TblTransTypes.Any(e => e.Id == id);
        }
    }
}
