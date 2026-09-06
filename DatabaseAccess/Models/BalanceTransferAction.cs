using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseAccess.Models
{
    public class BalanceTransferAction
    {
        public int OrderID { get; set; }

        [Required(ErrorMessage = "حقل المبلغ مطلوب.")]
        [Range(1, int.MaxValue, ErrorMessage = "يجب أن يكون المبلغ أكبر من صفر.")]
        public int BlncTrnsActAmount { get; set; }
        public int ProvId { get; set; }
        public int ProductID { get; set; }
        public int SenderUserId { get; set; }
        public int ReceiverUserId { get; set; }
        public int SenderRegID { get; set; }
        public int ReceiverRegID { get; set; }
        public int SenderBlncBefore { get; set; }
        public int ReceiverBlncBefore { get; set; }
        public int SenderBlncAfter { get; set; }
        public int ReceiverBlncAfter { get; set; }
        public int SenderDebtBefore { get; set; }
        public int SenderDebtAfter { get; set; }
        public int ReceiverDebtBefore { get; set; }
        public int ReceiverDebtAfter { get; set; }
        public DateOnly dateOnly { get; set; }
        public DateTime dateTime { get; set; }
        public string? Description { get; set; }

        [Required(ErrorMessage = "يرجى اختيار حساب المستقبل.")]
        [Display(Name = "حساب المستقبل")]
        public int SelectedReceiverRegId { get; set; }
    }
}
