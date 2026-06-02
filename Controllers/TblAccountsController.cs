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
    public class TblAccountsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblAccountsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblAccounts
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblAccounts.Include(t => t.ParentAccountIdFkNavigation).Include(t => t.UserIdFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblAccounts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblAccount = await _context.TblAccounts
                .Include(t => t.ParentAccountIdFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblAccount == null)
            {
                return NotFound();
            }

            return View(tblAccount);
        }

        // GET: TblAccounts/Create
        public IActionResult Create()
        {
            ViewData["ParentAccountIdFk"] = new SelectList(_context.TblAccounts, "Id", "Id");
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            return View();
        }

        // POST: TblAccounts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Code,UserIdFk,Indebtedness,AllwdCrdtLmt,ParentAccountIdFk,StartDate,IsActive")] TblAccount tblAccount)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblAccount);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ParentAccountIdFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblAccount.ParentAccountIdFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblAccount.UserIdFk);
            return View(tblAccount);
        }

        // GET: TblAccounts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblAccount = await _context.TblAccounts.FindAsync(id);
            if (tblAccount == null)
            {
                return NotFound();
            }
            ViewData["ParentAccountIdFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblAccount.ParentAccountIdFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblAccount.UserIdFk);
            return View(tblAccount);
        }

        // POST: TblAccounts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Code,UserIdFk,Indebtedness,AllwdCrdtLmt,ParentAccountIdFk,StartDate,IsActive")] TblAccount tblAccount)
        {
            if (id != tblAccount.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblAccount);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblAccountExists(tblAccount.Id))
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
            ViewData["ParentAccountIdFk"] = new SelectList(_context.TblAccounts, "Id", "Id", tblAccount.ParentAccountIdFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblAccount.UserIdFk);
            return View(tblAccount);
        }

        // GET: TblAccounts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblAccount = await _context.TblAccounts
                .Include(t => t.ParentAccountIdFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblAccount == null)
            {
                return NotFound();
            }

            return View(tblAccount);
        }

        // POST: TblAccounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblAccount = await _context.TblAccounts.FindAsync(id);
            if (tblAccount != null)
            {
                _context.TblAccounts.Remove(tblAccount);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblAccountExists(int id)
        {
            return _context.TblAccounts.Any(e => e.Id == id);
        }
    }
}
