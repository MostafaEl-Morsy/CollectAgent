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
    public class TblUserTypesController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblUserTypesController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblUserTypes
        public async Task<IActionResult> Index()
        {
            return View(await _context.TblUserTypes.ToListAsync());
        }

        // GET: TblUserTypes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblUserType = await _context.TblUserTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblUserType == null)
            {
                return NotFound();
            }

            return View(tblUserType);
        }

        // GET: TblUserTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: TblUserTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserTypeName,IsActive")] TblUserType tblUserType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblUserType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tblUserType);
        }

        // GET: TblUserTypes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblUserType = await _context.TblUserTypes.FindAsync(id);
            if (tblUserType == null)
            {
                return NotFound();
            }
            return View(tblUserType);
        }

        // POST: TblUserTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserTypeName,IsActive")] TblUserType tblUserType)
        {
            if (id != tblUserType.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblUserType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblUserTypeExists(tblUserType.Id))
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
            return View(tblUserType);
        }

        // GET: TblUserTypes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblUserType = await _context.TblUserTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblUserType == null)
            {
                return NotFound();
            }

            return View(tblUserType);
        }

        // POST: TblUserTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblUserType = await _context.TblUserTypes.FindAsync(id);
            if (tblUserType != null)
            {
                _context.TblUserTypes.Remove(tblUserType);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblUserTypeExists(int id)
        {
            return _context.TblUserTypes.Any(e => e.Id == id);
        }
    }
}
