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
    public class TblBranchesController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblBranchesController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblBranches
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblBranches.Include(t => t.CompanyFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblBranches/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblBranch = await _context.TblBranches
                .Include(t => t.CompanyFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblBranch == null)
            {
                return NotFound();
            }

            return View(tblBranch);
        }

        // GET: TblBranches/Create
        public IActionResult Create()
        {
            ViewData["CompanyFk"] = new SelectList(_context.TblCompanies, "Id", "Name");
            return View();
        }

        // POST: TblBranches/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,ContactNo,Address,CompanyFk,IsActive")] TblBranch tblBranch)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblBranch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CompanyFk"] = new SelectList(_context.TblCompanies, "Id", "Name", tblBranch.CompanyFk);
            return View(tblBranch);
        }

        // GET: TblBranches/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblBranch = await _context.TblBranches.FindAsync(id);
            if (tblBranch == null)
            {
                return NotFound();
            }
            ViewData["CompanyFk"] = new SelectList(_context.TblCompanies, "Id", "Name", tblBranch.CompanyFk);
            return View(tblBranch);
        }

        // POST: TblBranches/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,ContactNo,Address,CompanyFk,IsActive")] TblBranch tblBranch)
        {
            if (id != tblBranch.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblBranch);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblBranchExists(tblBranch.Id))
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
            ViewData["CompanyFk"] = new SelectList(_context.TblCompanies, "Id", "Name", tblBranch.CompanyFk);
            return View(tblBranch);
        }

        // GET: TblBranches/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblBranch = await _context.TblBranches
                .Include(t => t.CompanyFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblBranch == null)
            {
                return NotFound();
            }

            return View(tblBranch);
        }

        // POST: TblBranches/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblBranch = await _context.TblBranches.FindAsync(id);
            if (tblBranch != null)
            {
                _context.TblBranches.Remove(tblBranch);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblBranchExists(int id)
        {
            return _context.TblBranches.Any(e => e.Id == id);
        }
    }
}
