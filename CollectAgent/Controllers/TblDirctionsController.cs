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
    public class TblDirctionsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblDirctionsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblDirctions
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblDirctions.Include(t => t.DistrectFkNavigation).Include(t => t.GovernateFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblDirctions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDirction = await _context.TblDirctions
                .Include(t => t.DistrectFkNavigation)
                .Include(t => t.GovernateFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDirction == null)
            {
                return NotFound();
            }

            return View(tblDirction);
        }

        // GET: TblDirctions/Create
        public IActionResult Create()
        {
            ViewData["DistrectFk"] = new SelectList(_context.TblDistricts, "Id", "DistrictName");
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName");
            return View();
        }

        // POST: TblDirctions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,RouteName,GovernateFk,DistrectFk,IsActive")] TblDirction tblDirction)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblDirction);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["DistrectFk"] = new SelectList(_context.TblDistricts, "Id", "DistrictName", tblDirction.DistrectFk);
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName", tblDirction.GovernateFk);
            return View(tblDirction);
        }

        // GET: TblDirctions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDirction = await _context.TblDirctions.FindAsync(id);
            if (tblDirction == null)
            {
                return NotFound();
            }
            ViewData["DistrectFk"] = new SelectList(_context.TblDistricts, "Id", "DistrictName", tblDirction.DistrectFk);
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName", tblDirction.GovernateFk);
            return View(tblDirction);
        }

        // POST: TblDirctions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RouteName,GovernateFk,DistrectFk,IsActive")] TblDirction tblDirction)
        {
            if (id != tblDirction.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblDirction);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblDirctionExists(tblDirction.Id))
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
            ViewData["DistrectFk"] = new SelectList(_context.TblDistricts, "Id", "DistrictName", tblDirction.DistrectFk);
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName", tblDirction.GovernateFk);
            return View(tblDirction);
        }

        // GET: TblDirctions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDirction = await _context.TblDirctions
                .Include(t => t.DistrectFkNavigation)
                .Include(t => t.GovernateFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDirction == null)
            {
                return NotFound();
            }

            return View(tblDirction);
        }

        // POST: TblDirctions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblDirction = await _context.TblDirctions.FindAsync(id);
            if (tblDirction != null)
            {
                _context.TblDirctions.Remove(tblDirction);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblDirctionExists(int id)
        {
            return _context.TblDirctions.Any(e => e.Id == id);
        }
    }
}
