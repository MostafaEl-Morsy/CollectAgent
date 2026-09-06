using System.ComponentModel.DataAnnotations;

namespace CollectAgent.ViewModels
{
    public class AddDelegateViewModel : BaseUserViewModel
    {
        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [Display(Name = "اسم المستخدم")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "كلمة السر مطلوبة")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة السر")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة السر")]
        [Compare("Password", ErrorMessage = "كلمة السر وتأكيدها غير متطابقتين.")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "يرجى اختيار صلاحية المستخدم")]
        [Display(Name = "صلاحية المستخدم")]
        public int UserTypeFk { get; set; }

        //[Required(ErrorMessage = "يرجى اختيار مزود الخدمة")]
        //[Display(Name = "مزود الخدمة")]
        //public int Provider_fk { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "تاريخ نهاية الاشتراك")]
        public DateOnly? SubEndDate { get; set; }
    }
}
