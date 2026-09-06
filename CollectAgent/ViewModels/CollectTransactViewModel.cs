// CollectTransactViewModel.cs (يبقى كما هو تقريبًا)
using System;
using System.Collections.Generic;
using DatabaseAccess.Models; // تأكد من وجود using الصحيح

public class CollectTransactViewModel
{
    // لن نستخدم هذه القائمة مباشرة، لأن البيانات ستُحمّل بـ AJAX
    // public IEnumerable<TblDebtTrnsAct> Transactions { get; set; }

    // سنستخدم هذه لتحديد القيم الأولية في حقول التاريخ بالـ View
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public CollectTransactViewModel()
    {
        FromDate = DateOnly.FromDateTime(DateTime.Today);
        ToDate = DateOnly.FromDateTime(DateTime.Today);
    }
}