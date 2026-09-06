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
    public class TblProviderAccOrBanksController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblProviderAccOrBanksController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblProviderAccOrBanks
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblProviderAccOrBanks.Include(t => t.ProviderFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblProviderAccOrBanks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProviderAccOrBank = await _context.TblProviderAccOrBanks
                .Include(t => t.ProviderFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProviderAccOrBank == null)
            {
                return NotFound();
            }

            return View(tblProviderAccOrBank);
        }

        // GET: TblProviderAccOrBanks/Create
        public IActionResult Create()
        {
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode");
            return View();
        }

        // POST: TblProviderAccOrBanks/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ProviderAccountNo,ProviderFk,IsActive")] TblProviderAccOrBank tblProviderAccOrBank)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblProviderAccOrBank);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblProviderAccOrBank.ProviderFk);
            return View(tblProviderAccOrBank);
        }

        // GET: TblProviderAccOrBanks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProviderAccOrBank = await _context.TblProviderAccOrBanks.FindAsync(id);
            if (tblProviderAccOrBank == null)
            {
                return NotFound();
            }
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblProviderAccOrBank.ProviderFk);
            return View(tblProviderAccOrBank);
        }

        // POST: TblProviderAccOrBanks/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProviderAccountNo,ProviderFk,IsActive")] TblProviderAccOrBank tblProviderAccOrBank)
        {
            if (id != tblProviderAccOrBank.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblProviderAccOrBank);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblProviderAccOrBankExists(tblProviderAccOrBank.Id))
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
            ViewData["ProviderFk"] = new SelectList(_context.TblProviders, "Id", "ProviderCode", tblProviderAccOrBank.ProviderFk);
            return View(tblProviderAccOrBank);
        }

        // GET: TblProviderAccOrBanks/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblProviderAccOrBank = await _context.TblProviderAccOrBanks
                .Include(t => t.ProviderFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblProviderAccOrBank == null)
            {
                return NotFound();
            }

            return View(tblProviderAccOrBank);
        }

        // POST: TblProviderAccOrBanks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblProviderAccOrBank = await _context.TblProviderAccOrBanks.FindAsync(id);
            if (tblProviderAccOrBank != null)
            {
                _context.TblProviderAccOrBanks.Remove(tblProviderAccOrBank);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblProviderAccOrBankExists(int id)
        {
            return _context.TblProviderAccOrBanks.Any(e => e.Id == id);
        }
    }
}
