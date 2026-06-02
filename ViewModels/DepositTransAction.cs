using System;
using System.Collections.Generic; // Required for IEnumerable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace CollectAgent.ViewModels
{
    public class DepositTransAction : IValidatableObject
    {
        public int Id { get; set; }
        public string? ReferenceCode { get; set; }

        //[Required(ErrorMessage = "الرجاء اختيار الشركة المودع لها")]
        [Display(Name = "الشركة")] // هذا يغير اسم الحقل في رسائل الخطأ الافتراضية
        public int? ProviderID { get; set; }

        public int ProviderRegID { get; set; }

        //[Required(ErrorMessage = "الرجاء اختيار المستفيد")]
        [Display(Name = "المستفيد")]
        public int? DepositTo { get; set; }

        //[Required(ErrorMessage = "الرجاء إدخال رقم حساب الإيداع")]
        [Display(Name = "رقم حساب الإيداع")]
        public string? DepositAccNo { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-dd}")]
        [Display(Name = "التاريخ")]
        public DateOnly Date { get; set; }

        public DateTime DateTimeLocal { get; set; }

        [NotMapped]
        //[Required(ErrorMessage = "الرجاء اختيار صورة الإيداع")]
        [Display(Name = "صورة الإيداع")]
        public IFormFile? DepositPic { get; set; }

        public string? DepositPhoto { get; set; }

        // سنجعل هذا الحقل مطلوباً إذا كان المبلغ أكبر من صفر
        [Range(1, int.MaxValue, ErrorMessage = "يجب أن يكون مبلغ الإيداع أكبر من صفر")]
        [Display(Name = "مبلغ الإيداع")]
        public int DepositAmount { get; set; }
        public Nullable<int> Own500 { get; set; }
        public Nullable<int> Own200 { get; set; }
        public Nullable<int> Own100 { get; set; }
        public Nullable<int> Own50 { get; set; }
        public Nullable<int> Own20 { get; set; }
        public Nullable<int> Own10 { get; set; }
        public Nullable<int> Own5 { get; set; }
        public int TtlAmntOwned { get; set; }
        public Nullable<int> LE500 { get; set; }
        public Nullable<int> LE200 { get; set; }
        public Nullable<int> LE100 { get; set; }
        public Nullable<int> LE50 { get; set; }
        public Nullable<int> LE20 { get; set; }
        public Nullable<int> LE10 { get; set; }
        public Nullable<int> LE5 { get; set; }
        public int DepositType { get; set; }
        public int UserID { get; set; }
        public int StatusID { get; set; }

        // ✅ الخطوة 3: أضف هذه الدالة التي تحتوي على منطق التحقق المشروط
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // التحقق من الحقول البنكية (فقط للنوع 1 و 2)
            if (DepositType == 1 || DepositType == 2)
            {
                if (!ProviderID.HasValue)
                {
                    yield return new ValidationResult("الرجاء اختيار الشركة المودع لها", new[] { nameof(ProviderID) });
                }
                if (string.IsNullOrWhiteSpace(DepositAccNo))
                {
                    yield return new ValidationResult("الرجاء إدخال رقم حساب الإيداع", new[] { nameof(DepositAccNo) });
                }
                if (DepositPic == null)
                {
                    yield return new ValidationResult("الرجاء اختيار صورة الإيداع", new[] { nameof(DepositPic) });
                }
            }

            // التحقق من حقل المستفيد (فقط للنوع 2 و 3)
            if (DepositType == 2 || DepositType == 3)
            {
                if (!DepositTo.HasValue)
                {
                    yield return new ValidationResult("الرجاء اختيار المستفيد", new[] { nameof(DepositTo) });
                }
            }

            // التحقق من المبلغ (دائماً)
            if (DepositAmount <= 0)
            {
                yield return new ValidationResult("يجب أن يكون مبلغ الإيداع أكبر من صفر", new[] { nameof(DepositAmount) });
            }
        }
    }
}
