using DatabaseAccess.Models;
using System.Collections.Generic;

public class CashBalanceViewModel
{
    public int CurrentUserId { get; set; }
    public int CurrentUserType { get; set; }
    public int? MerchantId { get; set; } // <-- يجب ان يكون Nullable `int?` ليقبل القيمة الفارغة
    public IEnumerable<TblServProvReg> Wallets { get; set; }
}