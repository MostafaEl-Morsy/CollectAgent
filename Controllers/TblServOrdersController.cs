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
    public class TblServOrdersController : Controller
    {
        private readonly CollectAgentDBContext _context;

        public TblServOrdersController(CollectAgentDBContext context)
        {
            _context = context;
        }

        // GET: TblServOrders
        public async Task<IActionResult> Index()
        {
            var collectAgentDBContext = _context.TblServOrders.Include(t => t.ServProdFkNavigation).Include(t => t.StatusFkNavigation).Include(t => t.UserIdFkNavigation);
            return View(await collectAgentDBContext.ToListAsync());
        }

        // GET: TblServOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblServOrder = await _context.TblServOrders
                .Include(t => t.ServProdFkNavigation)
                .Include(t => t.StatusFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblServOrder == null)
            {
                return NotFound();
            }

            return View(tblServOrder);
        }

        // GET: TblServOrders/Create
        public IActionResult Create()
        {
            ViewData["ServProdFk"] = new SelectList(_context.TblServProducts, "Id", "ServProdCode");
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName");
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo");
            return View();
        }

        // POST: TblServOrders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserIdFk,OrderNo,ServProdFk,IndebtBefore,BlncAmntOrder,SellPrice,Fees,TotalPrice,IndebtAfter,TransDate,DateAndTime,BillingNo,StatusFk")] TblServOrder tblServOrder)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tblServOrder);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ServProdFk"] = new SelectList(_context.TblServProducts, "Id", "ServProdCode", tblServOrder.ServProdFk);
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblServOrder.StatusFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServOrder.UserIdFk);
            return View(tblServOrder);
        }

        // GET: TblServOrders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblServOrder = await _context.TblServOrders.FindAsync(id);
            if (tblServOrder == null)
            {
                return NotFound();
            }
            ViewData["ServProdFk"] = new SelectList(_context.TblServProducts, "Id", "ServProdCode", tblServOrder.ServProdFk);
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblServOrder.StatusFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServOrder.UserIdFk);
            return View(tblServOrder);
        }

        // POST: TblServOrders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserIdFk,OrderNo,ServProdFk,IndebtBefore,BlncAmntOrder,SellPrice,Fees,TotalPrice,IndebtAfter,TransDate,DateAndTime,BillingNo,StatusFk")] TblServOrder tblServOrder)
        {
            if (id != tblServOrder.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tblServOrder);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TblServOrderExists(tblServOrder.Id))
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
            ViewData["ServProdFk"] = new SelectList(_context.TblServProducts, "Id", "ServProdCode", tblServOrder.ServProdFk);
            ViewData["StatusFk"] = new SelectList(_context.TblStatuses, "Id", "StatusName", tblServOrder.StatusFk);
            ViewData["UserIdFk"] = new SelectList(_context.TblUsers, "Id", "ContactNo", tblServOrder.UserIdFk);
            return View(tblServOrder);
        }

        // GET: TblServOrders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tblServOrder = await _context.TblServOrders
                .Include(t => t.ServProdFkNavigation)
                .Include(t => t.StatusFkNavigation)
                .Include(t => t.UserIdFkNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tblServOrder == null)
            {
                return NotFound();
            }

            return View(tblServOrder);
        }

        // POST: TblServOrders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tblServOrder = await _context.TblServOrders.FindAsync(id);
            if (tblServOrder != null)
            {
                _context.TblServOrders.Remove(tblServOrder);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TblServOrderExists(int id)
        {
            return _context.TblServOrders.Any(e => e.Id == id);
        }
    }
}
