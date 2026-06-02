using System.ComponentModel.DataAnnotations;

namespace CollectAgent.Models
{
    public class CollectTransferAction
    {
        public int Id { get; set; }
        //public string ReferenceCode { get; set; }
        public int CollectFromID { get; set; }
        public Nullable<int> CollectFromRegId { get; set; }
        public int CollectFromDebtBfor { get; set; }
        public int CollectFromDebtAftr { get; set; }
        public int BlnceTransfer { get; set; }
        public int BlncReturned { get; set; }
        public int CollectorBlncBfor { get; set; }
        public int CollectorBlncAftr { get; set; }
        public int CollectFromBlncBfor { get; set; }
        public int CollectFromBlncAftr { get; set; }
        public int User { get; set; }
        public int CollectorID { get; set; }
        public int CollectorRegId { get; set; }
        public int CollectorDebtBfor { get; set; }
        public int CollectorDebtAftr { get; set; }
        public int CollectorDrwrBfor { get; set; }
        public int CollectorDrwrAftr { get; set; }
        [Display(Name = "اختر حساب المورد")]
        public int? SupplierRegistrationId { get; set; }
        public int Be500 { get; set; }
        public int Be200 { get; set; }
        public int Be100 { get; set; }
        public int BE50 { get; set; }
        public int Be20 { get; set; }
        public int Be10 { get; set; }
        public int Be5 { get; set; }
        public int TotlMoneyOwned { get; set; }
        public Nullable<int> Le500 { get; set; }
        public Nullable<int> Le200 { get; set; }
        public Nullable<int> Le100 { get; set; }
        public Nullable<int> Le50 { get; set; }
        public Nullable<int> Le20 { get; set; }
        public Nullable<int> Le10 { get; set; }
        public Nullable<int> Le5 { get; set; }
        public int TotlMoneyTranact { get; set; }
        public int Af500 { get; set; }
        public int Af200 { get; set; }
        public int Af100 { get; set; }
        public int Af50 { get; set; }
        public int Af20 { get; set; }
        public int Af10 { get; set; }
        public int Af5 { get; set; }
        public int TotlMoneyAftr { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateOnly TransDate { get; set; }
        public DateTime DateAndTime { get; set; }
        public DateTime TransactionTimestamp { get; set; }
    }
}
