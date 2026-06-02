using NuGet.Protocol.Core.Types;

namespace CollectAgent.Models
{
    public class ServProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal? SellPrice { get; set; }
        public string ProviderName { get; set; } // معلومة إضافية مفيدة للعرض
    }
}
// ViewModel لتمثيل المنتج الواحد في البطاقة
public class ServiceProductCardViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public decimal? SellPrice { get; set; } // <-- إضافة السعر
    public string ProviderLogoUrl { get; set; }
    public string? prodRegID { get; set; }
    public int ProviderId { get; set; } // نحتاجها لإنشاء الرابط
    //public int? MerchantId { get; set; } // <-- معرف التاجر (اختياري)

}

// ViewModel الرئيسي للصفحة بأكملها
public class PreCreateViewModel
{
    public string PageTitle { get; set; }
    public string BackButtonUrl { get; set; }
    public string ProviderName { get; set; } // <-- اسم مزود الخدمة
    public int? MerchantId { get; set; } // <-- معرف التاجر (اختياري)
    public string? MerchantName { get; set; } // <-- اسم التاجر (اختياري)
    public List<ServiceProductCardViewModel> Products { get; set; }

    public PreCreateViewModel()
    {
        Products = new List<ServiceProductCardViewModel>();
    }
}