// Models/UserStatsViewModel.cs
namespace CollectAgent.Models
{
    public class UserStatsViewModel
    {
        public double Balance { get; set; }           // رصيد الأجهزة (POS)
        public double Inventory { get; set; }          // صافي المركز المالي النهائي
        public double Bank { get; set; }              // المحافظ البنكية
        public double Cash { get; set; }              // محافظ الكاش
        public double Debtors { get; set; }           // مدينين (تجار عليهم مبالغ)
        public double Creditors { get; set; }         // دائنين (تجار لهم مبالغ)
        public double Drawer { get; set; }            // النقدية بالخزينة
        public double TotalAssets { get; set; }       // إجمالي الأصول
        public double TotalLiabilities { get; set; } // إجمالي الالتزامات
        public double AccountsBalance { get; set; }   // إجمالي أرصدة الحسابات (POS + كاش + بنك)
    }
}