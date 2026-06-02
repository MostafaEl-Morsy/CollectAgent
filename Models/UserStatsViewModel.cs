// Models/UserStatsViewModel.cs
namespace CollectAgent.Models
{
    public class UserStatsViewModel
    {
        public double Balance { get; set; } // الرصيد (POS)
        public double Inventory { get; set; } // إجمالي العهدة
        public double Bank { get; set; } // البنك / انستا باي
        public double Cash { get; set; } // محافظ الكاش
        public double Indebt { get; set; } // النقدية (Indebt)
        public double Debtors { get; set; }  // مدينين (تجار عليهم مبالغ)
        public double Creditors { get; set; }  // دائنين (تجار لهم مبالغ)
    }
}
