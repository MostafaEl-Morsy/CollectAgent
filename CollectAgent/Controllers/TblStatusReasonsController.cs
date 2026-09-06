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
    public class TblStatusReasonsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblStatusReasonsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblStatusReasons
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblStatusReasons.Include(t => t.StatusIdFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblStatusReasons/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblStatusReason = await _context.TblStatusReasons
                .Include(t => t.StatusIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblStatusReason == null)
            {
                return NotFound();
            }

            return View(tblStatusReason);
        }

        // GET: TblStatusReasons/Create
        public IActionResult Create()
        {
            ViewData["StatusIdFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName");
            return View();
        }

        // POST: TblStatusReasons/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,StatusReason,StatusIdFk,IsActive")] TblStatusReason tblStatusReason)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblStatusReason);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["StatusIdFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblStatusReason.StatusIdFk);
            return View(tblStatusReason);
        }

        // GET: TblStatusReasons/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblStatusReason = await _context.TblStatusReasons.FindAsync(id);
            if (tblStatusReason == null)
            {
                return NotFound();
            }
            ViewData["StatusIdFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblStatusReason.StatusIdFk);
            return View(tblStatusReason);
        }

        // POST: TblStatusReasons/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,StatusReason,StatusIdFk,IsActive")] TblStatusReason tblStatusReason)
        {
            if (id != tblStatusReason.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblStatusReason);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblStatusReasonExists(tblStatusReason.Id))
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
            ViewData["StatusIdFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblStatusReason.StatusIdFk);
            return View(tblStatusReason);
        }

        // GET: TblStatusReasons/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblStatusReason = await _context.TblStatusReasons
                .Include(t => t.StatusIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblStatusReason == null)
            {
                return NotFound();
            }

            return View(tblStatusReason);
        }

        // POST: TblStatusReasons/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblStatusReason = await _context.TblStatusReasons.FindAsync(id);
            if (tblStatusReason != null)
            {
                _context.TblStatusReasons.Remove(tblStatusReason);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblStatusReasonExists(int id)
        {
            return _context.TblStatusReasons.Any(e => e.Id == id);
        }
    }
}
