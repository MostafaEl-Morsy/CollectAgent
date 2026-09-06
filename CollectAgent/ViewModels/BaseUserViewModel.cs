using System.ComponentModel.DataAnnotations;

namespace CollectAgent.ViewModels
{
    public abstract class BaseUserViewModel
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم بالكامل")]
        public string FullName { get; set; }

        //[Required(ErrorMessage = "اسم المحل مطلوب")]
        //[Display(Name = "اسم المحل / المسئول")]
        //public string ShopName { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Display(Name = "رقم التواصل")]
        public string ContactNo { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المحافظة")]
        [Display(Name = "المحافظة")]
        public int Governate_FK { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المنطقة")]
        [Display(Name = "المنطقة")]
        public int Distrect_FK { get; set; }

        [Display(Name = "العنوان")]
        public string Address { get; set; }

        [Required(ErrorMessage = " اسم/رقم نقطة البيع")]
        [Display(Name = "اسم/رقم نقطة البيع")]
        public string AccNameNO { get; set; }

        [Display(Name = "مفعل")]
        public bool IsActive { get; set; } = true;
    }
}
