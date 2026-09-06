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
    public class TblGovernatesController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblGovernatesController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblGovernates
        public async Task<IActionResult> Index()
        {
            return View(await _context.TblGovernates.ToListAsync());
        }

        // GET: TblGovernates/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblGovernate = await _context.TblGovernates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblGovernate == null)
            {
                return NotFound();
            }

            return View(tblGovernate);
        }

        // GET: TblGovernates/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: TblGovernates/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,GovernateName,IsActive")] TblGovernate tblGovernate)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblGovernate);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tblGovernate);
        }

        // GET: TblGovernates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblGovernate = await _context.TblGovernates.FindAsync(id);
            if (tblGovernate == null)
            {
                return NotFound();
            }
            return View(tblGovernate);
        }

        // POST: TblGovernates/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,GovernateName,IsActive")] TblGovernate tblGovernate)
        {
            if (id != tblGovernate.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblGovernate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblGovernateExists(tblGovernate.Id))
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
            return View(tblGovernate);
        }

        // GET: TblGovernates/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblGovernate = await _context.TblGovernates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblGovernate == null)
            {
                return NotFound();
            }

            return View(tblGovernate);
        }

        // POST: TblGovernates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblGovernate = await _context.TblGovernates.FindAsync(id);
            if (tblGovernate != null)
            {
                _context.TblGovernates.Remove(tblGovernate);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblGovernateExists(int id)
        {
            return _context.TblGovernates.Any(e => e.Id == id);
        }
    }
}
