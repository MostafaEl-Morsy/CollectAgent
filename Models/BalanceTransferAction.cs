namespace CollectAgent.Models
{
    public class BalanceTransferAction
    {
        public int OrderID { get; set; }
        public int BlncTrnsActAmount { get; set; }
        public int ProvId { get; set; }
        public int ProductID { get; set; }
        public int SenderUserId { get; set; }
        public int ReceiverUserId { get; set; }
        public int SenderRegID { get; set; }
        public int ReceiverRegID { get; set; }
        public float SenderBlncBefore { get; set; }
        public float ReceiverBlncBefore { get; set; }
        public float SenderBlncAfter { get; set; }
        public float ReceiverBlncAfter { get; set; }
        public double SenderDebtBefore { get; set; }
        public double SenderDebtAfter { get; set; }
        public double ReceiverDebtBefore { get; set; }
        public double ReceiverDebtAfter { get; set; }
        public DateTime dateTime { get; set; }
    }
}
