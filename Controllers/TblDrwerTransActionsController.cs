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
    public class TblDrwerTransActionsController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblDrwerTransActionsController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblDrwerTransActions
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblDrwerTransActions.Include(t => t.DrawerIdFkNavigation).Include(t => t.TransType);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblDrwerTransActions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDrwerTransAction = await _context.TblDrwerTransActions
                .Include(t => t.DrawerIdFkNavigation)
                .Include(t => t.TransType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDrwerTransAction == null)
            {
                return NotFound();
            }

            return View(tblDrwerTransAction);
        }

        // GET: TblDrwerTransActions/Create
        public IActionResult Create()
        {
            ViewData["DrawerIdFk"] = new SelectList(_context.TblDrawers, "Id", "Code");
            ViewData["TransTypeId"] = new SelectList(_context.TblTransTypes, "Id", "TransTypeName");
            return View();
        }

        // POST: TblDrwerTransActions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,RefCode,DrawerIdFk,Own500,Own200,Own100,Own50,Own20,Own10,Own5,TtlAmntOwned,Le500,Le200,Le100,Le50,Le20,Le10,Le5,TtlAmntTrnsAct,After500,After200,After100,After50,After20,After10,After5,TtlAmntAfter,TransDate,DateAndTime,TransTypeId,Description")] TblDrwerTransAction tblDrwerTransAction)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblDrwerTransAction);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["DrawerIdFk"] = new SelectList(_context.TblDrawers, "Id", "Code", tblDrwerTransAction.DrawerIdFk);
            ViewData["TransTypeId"] = new SelectList(_context.TblTransTypes, "Id", "TransTypeName", tblDrwerTransAction.TransTypeId);
            return View(tblDrwerTransAction);
        }

        // GET: TblDrwerTransActions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDrwerTransAction = await _context.TblDrwerTransActions.FindAsync(id);
            if (tblDrwerTransAction == null)
            {
                return NotFound();
            }
            ViewData["DrawerIdFk"] = new SelectList(_context.TblDrawers, "Id", "Code", tblDrwerTransAction.DrawerIdFk);
            ViewData["TransTypeId"] = new SelectList(_context.TblTransTypes, "Id", "TransTypeName", tblDrwerTransAction.TransTypeId);
            return View(tblDrwerTransAction);
        }

        // POST: TblDrwerTransActions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RefCode,DrawerIdFk,Own500,Own200,Own100,Own50,Own20,Own10,Own5,TtlAmntOwned,Le500,Le200,Le100,Le50,Le20,Le10,Le5,TtlAmntTrnsAct,After500,After200,After100,After50,After20,After10,After5,TtlAmntAfter,TransDate,DateAndTime,TransTypeId,Description")] TblDrwerTransAction tblDrwerTransAction)
        {
            if (id != tblDrwerTransAction.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblDrwerTransAction);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblDrwerTransActionExists(tblDrwerTransAction.Id))
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
            ViewData["DrawerIdFk"] = new SelectList(_context.TblDrawers, "Id", "Code", tblDrwerTransAction.DrawerIdFk);
            ViewData["TransTypeId"] = new SelectList(_context.TblTransTypes, "Id", "TransTypeName", tblDrwerTransAction.TransTypeId);
            return View(tblDrwerTransAction);
        }

        // GET: TblDrwerTransActions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblDrwerTransAction = await _context.TblDrwerTransActions
                .Include(t => t.DrawerIdFkNavigation)
                .Include(t => t.TransType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblDrwerTransAction == null)
            {
                return NotFound();
            }

            return View(tblDrwerTransAction);
        }

        // POST: TblDrwerTransActions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblDrwerTransAction = await _context.TblDrwerTransActions.FindAsync(id);
            if (tblDrwerTransAction != null)
            {
                _context.TblDrwerTransActions.Remove(tblDrwerTransAction);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblDrwerTransActionExists(int id)
        {
            return _context.TblDrwerTransActions.Any(e => e.Id == id);
        }
    }
}
