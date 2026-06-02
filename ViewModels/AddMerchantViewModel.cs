using System.ComponentModel.DataAnnotations;

namespace CollectAgent.ViewModels
{
    public class AddMerchantViewModel : BaseUserViewModel
    {
        [Required(ErrorMessage = "يرجى كتابة مديونية التاجر")]
        [Display(Name = "مديونية التاجر")]
        public int? Indebtedness { get; set; }

        // التاجر ليس بحاجة لـ Provider_fk كحقل مطلوب هنا
        // لكننا سنحتاجه في الخلفية
        //public int Provider_fk { get; set; }
    }
}
