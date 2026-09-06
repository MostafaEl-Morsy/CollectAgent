using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
//using System.Web.Mvc; // Required for SelectList

namespace CollectAgent.ViewModels
{
    public class AddUserViewModel
    {
        public int Id { get; set; }// ضروري لصفحة التعديل
        // قسم بيانات الدخول
        [Display(Name = "اسم المستخدم")]
        public string? UserName { get; set; }// ? للسماح بـ null

        [DataType(DataType.Password)]
        [Display(Name = "كلمة السر")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة السر")]
        [Compare("Password", ErrorMessage = "كلمة السر وتأكيدها غير متطابقتين.")]
        public string? ConfirmPassword { get; set; }

        // قسم البيانات الشخصية وبيانات المحل
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم بالكامل")]
        public string FullName { get; set; } = string.Empty; // تهيئة لمنع NullReference

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Display(Name = "رقم التواصل")]
        public string ContactNo { get; set; } = string.Empty; // تهيئة لمنع NullReference

        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [Display(Name = "الشارع")]
        public string? Street { get; set; } = string.Empty;

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "يرجى إدخال بريد إلكتروني صالح")]
        public string? Email { get; set; }

        [Display(Name = "رقم البطاقة")]
        [RegularExpression(@"^\d{14}$", ErrorMessage = "رقم البطاقة يجب أن يكون مكونًا من 14 رقمًا.")]
        public string? Naid { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المحافظة")]
        [Display(Name = "المحافظة")]
        public int Governate_FK { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المنطقة")]
        [Display(Name = "المنطقة")]
        public int Distrect_FK { get; set; }

        // قسم بيانات الحساب والنظام
        [Required(ErrorMessage = "من فضلك اختر مسئول الحساب")]
        [Display(Name = "مسئول الحساب")]
        public int Representative { get; set; }

        [Display(Name = "اسم الشركة")]
        public int? CompanyFk { get; set; }

        [Display(Name = "اسم الفرع")]
        public int? BranchFk { get; set; }

        [Required(ErrorMessage = "يرجى اختيار صلاحية المستخدم")]
        [Display(Name = "صلاحية المستخدم")]
        public int UserTypeFk { get; set; }

        [Display(Name = "كود التسجيل")]
        public string? Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى كتابة مديونية التاجر")]
        [Display(Name = "مديونية التاجر")]
        public Nullable<int> Indebtedness { get; set; } // Nullable

        [Display(Name = "الحد الائتماني المسموح")]
        public int AllwdCrdtLmt { get; set; }

        [Display(Name = "مفعل")]
        public bool IsActive { get; set; } = true; // قيمة افتراضية

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "تاريخ بداية العمل")]
        public DateOnly ApprvDate { get; set; }

        // تاريخ نهاية الاشتراك (اختياري)
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "تاريخ نهاية الاشتراك")]
        public DateOnly SubEndDate { get; set; }

        // --- قوائم منسدلة لتعبئة الواجهة ---
        // قوائم منسدلة (يمكن أن تكون Nullable لأنها تُملأ أحياناً في الكنترولر)
        // استخدام [ValidateNever] لتجاهل التحقق من هذه الخصائص
        [ValidateNever]
        public IEnumerable<SelectListItem>? Governates { get; set; }
        public IEnumerable<SelectListItem>? Districts { get; set; }
        public IEnumerable<SelectListItem>? UserTypes { get; set; }
        public IEnumerable<SelectListItem>? Companies { get; set; }
        public IEnumerable<SelectListItem>? Branches { get; set; }
    }
}
// OtherInformation: End of the code snippet from /path/to/ViewModels/AddUserViewModel.cs