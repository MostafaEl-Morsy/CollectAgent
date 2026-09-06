using CollectAgent.ViewModels;
using DatabaseAccess.Models;


namespace CollectAgent.Models
{
    public class UserService
    {
        private readonly CollectAgentDBContext _context;

        public UserService(CollectAgentDBContext context)
        {
            _context = context;
        }

        public bool CreateUserAndRelatedEntities(AddUserViewModel model, int currentUserType)
        {
            // استخدام Transaction لضمان أن كل العمليات تتم معاً أو لا تتم على الإطلاق
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // ملاحظة هامة: يجب تشفير كلمة المرور قبل حفظها
                    // string hashedPassword = HashPassword(model.Password);
                    var newUser = new TblUser
                    {
                        FullName = model.FullName,
                        ContactNo = model.ContactNo,
                        UserName = model.UserName,
                        Password = model.Password, // استبدل بـ hashedPassword
                        Photo = "0", // Placeholder, يجب تعديلها لاحقاً
                        Address = model.Address,
                        Street = model.Street?? "0",
                        DistrectFk = model.Distrect_FK,
                        GovernateFk = model.Governate_FK,
                        Naid = "0", // Placeholder, يجب تعديلها لاحقاً
                        Email = "0", // Placeholder, يجب تعديلها لاحقاً
                        StartDate = model.ApprvDate,
                        SubEndDate = model.SubEndDate,
                        UserTypeFk = model.UserTypeFk,
                        DirctionFk = 1, // يجب تعديل هذا بناءً على منطق التطبيق
                        Priority = 0, // يجب تعديل هذا بناءً على منطق التطبيق
                        CompanyFk = model.CompanyFk ?? 1,
                        BranchFk = model.BranchFk ?? 1,
                        ParentUser = model.Representative,
                        UserCode = model.Code?? model.FullName,
                        IsActive = model.IsActive
                    };
                    _context.TblUsers.Add(newUser);
                    _context.SaveChanges(); // الحفظ هنا ضروري للحصول على ID المستخدم الجديد

                    var account = new TblAccount
                    {
                        Code = model.Code?? model.FullName,
                        UserIdFk = newUser.Id,
                        Indebtedness = model.Indebtedness,
                        AllwdCrdtLmt = 0,
                        ParentAccountIdFk = 8, // يجب تعديل هذا بناءً على منطق التطبيق
                        StartDate = model.ApprvDate,
                        IsActive = model.IsActive
                    };
                    _context.TblAccounts.Add(account);

                    //var register = new TblRegistration
                    //{
                    //    AccNameNo = model.AccNameNO,
                    //    UserFk = newUser.Id,
                    //    Balance = 0,
                    //    StartDate = model.ApprvDate,
                    //    IsActive = model.IsActive
                    //};
                    //_context.TblRegistrations.Add(register);

                    if (currentUserType < 7)
                    {
                        var drawer = new TblDrawer
                        {
                            Code = model.Code?? model.FullName,
                            UserFk = newUser.Id,
                            Le500 = 0,
                            Le200 = 0,
                            Le100 = 0,
                            Le50 = 0,
                            Le20 = 0,
                            Le10 = 0,
                            Le5 = 0,
                            TtlAmntOwned = 0,
                            StartDate = model.ApprvDate,
                            IsActive = true
                        };
                        _context.TblDrawers.Add(drawer);
                    }

                    _context.SaveChanges(); // حفظ كل الكيانات المضافة (Account, Registration, Drawer)
                    transaction.Commit(); // تأكيد كل العمليات بنجاح
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback(); // في حالة حدوث أي خطأ، تراجع عن كل شيء
                    // الأفضل هو تسجيل الخطأ هنا باستخدام مكتبة مثل Serilog أو NLog
                    return false;
                }
            }
        }

        public bool UpdateUserAndRelatedEntities(AddUserViewModel model, int userIdToUpdate)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // 1. جلب المستخدم الحالي من قاعدة البيانات لضمان أننا نعدل على بيانات موجودة
                    var userToUpdate = _context.TblUsers.Find(userIdToUpdate);
                    if (userToUpdate == null)
                    {
                        return false; // المستخدم غير موجود
                    }
                    var accountToUpdate = _context.TblAccounts.FirstOrDefault(a => a.UserIdFk == userIdToUpdate);
                    var drawerToUpdate = _context.TblDrawers.FirstOrDefault(d => d.UserFk == userIdToUpdate);

                    // 2. تحديث البيانات الأساسية للمستخدم من النموذج
                    userToUpdate.FullName = model.FullName;
                    userToUpdate.ContactNo = model.ContactNo;
                    userToUpdate.Address = model.Address ?? "0"; // تأكد من التعامل مع null
                    userToUpdate.Street = model.Street ?? "0"; // تأكد من التعامل مع null
                    userToUpdate.DistrectFk = model.Distrect_FK;
                    userToUpdate.GovernateFk = model.Governate_FK;
                    userToUpdate.IsActive = model.IsActive;
                    userToUpdate.Email = model.Email ?? "0";
                    userToUpdate.Naid = model.Naid ?? "0";
                    userToUpdate.CompanyFk = model.CompanyFk ?? 1;
                    userToUpdate.BranchFk = model.BranchFk ?? 1;
                    userToUpdate.SubEndDate = model.SubEndDate; // يتم تحديثه دائماً من النموذج
                    userToUpdate.UserCode = model.Code ?? model.FullName;
                    // 3. تحديث اسم المستخدم فقط إذا تم إدخال قيمة جديدة
                    if (!string.IsNullOrWhiteSpace(model.UserName))
                    {
                        userToUpdate.UserName = model.UserName;
                    }
                    // إذا كان الحقل فارغاً، سيبقى اسم المستخدم القديم كما هو

                    if (model.UserTypeFk != 0) // تجنب القيمة الافتراضية
                    {
                        userToUpdate.UserTypeFk = model.UserTypeFk;
                    }

                    // 4. جلب الحساب المرتبط بالمستخدم
                    if (accountToUpdate != null)
                    {
                        if (model.Indebtedness.HasValue)
                        {
                            accountToUpdate.Indebtedness = model.Indebtedness;
                        }
                        // تحديث الحقول في جدول الحسابات
                        accountToUpdate.Code = model.Code?? model.FullName; // مثلاً، مزامنة اسم المستخدم
                        accountToUpdate.Indebtedness = model.Indebtedness; // يمكنك تعديل هذا بناءً على منطق التطبيق
                        accountToUpdate.AllwdCrdtLmt = model.AllwdCrdtLmt; // يمكنك تعديل هذا بناءً على منطق التطبيق
                        accountToUpdate.IsActive = model.IsActive; // مثلاً، مزامنة حالة التفعيل
                                                                          // يمكنك إضافة أي تحديثات أخرى هنا
                    }

                    //  جلب وتحديث TblDrawer
                    if (drawerToUpdate != null)
                    {
                        drawerToUpdate.Code = model.FullName; // مزامنة اسم المستخدم
                        drawerToUpdate.IsActive = userToUpdate.IsActive; // مزامنة حالة التفعيل
                                                                         // يمكنك إضافة أي تحديثات أخرى هنا
                    }

                    _context.SaveChanges(); // حفظ كل التغييرات
                    transaction.Commit(); // تأكيد العملية
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback(); // تراجع عن كل شيء في حالة حدوث خطأ
                                            // يمكنك هنا تسجيل الخطأ ex
                    return false;
                }
            }
        }
    }
}
