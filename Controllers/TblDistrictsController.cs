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
    public class TblDistrictsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblDistrictsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblDistricts
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblDistricts.Include(t => t.GovernateFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblDistricts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDistrict = await _context.TblDistricts
                .Include(t => t.GovernateFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDistrict == null)
            {
                return NotFound();
            }

            return View(tblDistrict);
        }

        // GET: TblDistricts/Create
        public IActionResult Create()
        {
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName");
            return View();
        }

        // POST: TblDistricts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,DistrictName,GovernateFk,IsActive")] TblDistrict tblDistrict)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblDistrict);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName", tblDistrict.GovernateFk);
            return View(tblDistrict);
        }

        // GET: TblDistricts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDistrict = await _context.TblDistricts.FindAsync(id);
            if (tblDistrict == null)
            {
                return NotFound();
            }
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName", tblDistrict.GovernateFk);
            return View(tblDistrict);
        }

        // POST: TblDistricts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,DistrictName,GovernateFk,IsActive")] TblDistrict tblDistrict)
        {
            if (id != tblDistrict.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblDistrict);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblDistrictExists(tblDistrict.Id))
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
            ViewData["GovernateFk"] = new SelectList(_context.TblGovernates, "Id", "GovernateName", tblDistrict.GovernateFk);
            return View(tblDistrict);
        }

        // GET: TblDistricts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDistrict = await _context.TblDistricts
                .Include(t => t.GovernateFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDistrict == null)
            {
                return NotFound();
            }

            return View(tblDistrict);
        }

        // POST: TblDistricts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblDistrict = await _context.TblDistricts.FindAsync(id);
            if (tblDistrict != null)
            {
                _context.TblDistricts.Remove(tblDistrict);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblDistrictExists(int id)
        {
            return _context.TblDistricts.Any(e => e.Id == id);
        }
    }
}
