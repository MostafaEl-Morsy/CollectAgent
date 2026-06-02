// Middleware/SessionRefreshMiddleware.cs
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class SessionRefreshMiddleware
{
    private readonly RequestDelegate _next;

    public SessionRefreshMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, CollectAgentDBContext dbContext)
    {
        // تحقق أولاً إذا كان المستخدم مسجل دخوله ولديه هوية
        if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
        {
            // تحقق إذا كانت الجلسة فارغة (لا تحتوي على اسم المستخدم مثلاً)
            if (string.IsNullOrEmpty(context.Session.GetString("FullName")))
            {
                // الجلسة فارغة، لذا لنقم بإعادة تعبئتها من قاعدة البيانات
                var userIdString = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int userId))
                {
                    var user = await dbContext.TblUsers.FindAsync(userId);
                    if (user != null)
                    {
                        // --- نفس الكود تقريباً من AccountController ---
                        var userReg = await dbContext.TblRegistrations.FirstOrDefaultAsync(r => r.UserFk == user.Id && r.IsActive == true);
                        var userAcc = await dbContext.TblAccounts.FirstOrDefaultAsync(a => a.UserIdFk == user.Id && a.IsActive == true);
                        double? sumWallets = await dbContext.TblServProvRegs.Where(p => p.UserFk == user.Id && p.IsActive == true).SumAsync(p => p.Balance);
                        double? sumBankWallets = await dbContext.TblServProvRegs.Where(p => p.UserFk == user.Id && p.IsActive == true).SumAsync(p => p.Balance);
                        double indebt = (double)(userAcc?.Indebtedness ?? 0) * -1;
                        double repBalance = (double)(userReg?.Balance ?? 0);
                        double inventory = repBalance + indebt + (sumWallets ?? 0) + (sumBankWallets ?? 0);

                        context.Session.SetString("UserID", user.Id.ToString());
                        context.Session.SetString("FullName", user.FullName);
                        context.Session.SetString("Balance", repBalance.ToString("N2"));
                        context.Session.SetString("Inventory", inventory.ToString("N2"));
                        context.Session.SetString("sumBankWallets", (sumBankWallets ?? 0).ToString("N2"));
                        context.Session.SetString("sumWallets", (sumWallets ?? 0).ToString("N2"));
                    }
                }
            }
        }

        // اسمح للطلب بإكمال مساره إلى الـ Controller
        
        await _next(context);
    }
}