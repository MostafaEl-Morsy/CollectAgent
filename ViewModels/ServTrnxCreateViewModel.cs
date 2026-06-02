// In folder: ViewModels/ServTrnxCreateViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace CollectAgent.ViewModels
{
    public class ServTrnxCreateViewModel
    {
        public int Id { get; set; }
        public string? OrderNo { get; set; } = string.Empty;
        public string? ReferenceCode { get; set; }
        public int ServProdId { get; set; }
        public int SenderRegId { get; set; }
        public int ServProvId { get; set; }
        public string? BillingNumTrx { get; set; }
        public int FeesTrx { get; set; }
        public int TotalFeesTrx { get; set; }
        public int IndebtAfter { get; set; }
        public DateOnly TransDate { get; set; }
        public DateTime DateAndTime { get; set; }
        public string? ConfirmPic { get; set; }
        public int UserID { get; set; }
        public int StatusId { get; set; }
        public int Emp { get; set; }
        // === بيانات مخفية نحتاجها للمعالجة في الخلفية ===
        public int ServProdFk { get; set; }
        public int SenderUserId { get; set; } // سيتم تعيينه من المستخدم الحالي
        public int MerchantId { get; set; }   // التاجر الذي تتم العملية لحسابه
        public int ProductType { get; set; }  // نوع المنتج (1=خصم, 2=إضافة) لتوجيه الحسابات في JS
        public int SellPriceTrx { get; set; } // سعر البيع للمنتج
        public int? SendrRegId { get; set; } // قد نحتاجه لاحقاً

        // === حقول سيقوم المستخدم بإدخالها ===
        [Required(ErrorMessage = "الرجاء إدخال رقم الموبايل أو الحساب")]
        [Display(Name = "رقـم الموبايل / الحـساب")]
        public string? CustomerNoTrx { get; set; }

        [Required(ErrorMessage = "الرجاء إدخال المبلغ")]
        [Range(1, double.MaxValue, ErrorMessage = "يجب أن يكون المبلغ أكبر من صفر")]
        [Display(Name = "المبلـغ")]
        public int BlncAmntOrderTrx { get; set; }
        // === بيانات للعرض والحساب ===
        [Display(Name = "المديونية قبل")]
        public int IndebtBefore { get; set; }
        public Nullable<int> Le500 { get; set; }
        public Nullable<int> Le200 { get; set; }
        public Nullable<int> Le100 { get; set; }
        public Nullable<int> Le50 { get; set; }
        public Nullable<int> Le20 { get; set; }
        public Nullable<int> Le10 { get; set; }
        public Nullable<int> Le5 { get; set; }
        public float TotlMoneyTranact { get; set; }
        // هذا الحقل سيتم حسابه وإرساله من الفورم
        public int TotalPriceTrx { get; set; }
        // === بيانات للعرض فقط (سيتم ملؤها في الـ Controller) ===
        public int SendrBlncBfr { get; set; } // الرصيد قبل

        // --- يمكن إضافة هذه الحقول للعرض في الهيدر ---
        public string? ProviderName { get; set; }
        public string? ProductName { get; set; }
        public string? ProviderLogoUrl { get; set; }
    }
}