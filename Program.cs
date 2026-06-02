using DatabaseAccess.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

// ✅ *** أضف هذا السطر هنا لتحديد ترخيص EPPlus مرة واحدة للتطبيق كله ***
// تجاهل التحذير الخاص بأن هذه الخاصية قديمة
ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial; // للاستخدام غير التجاري

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<CollectAgentDBContext>(options =>
    options.UseSqlServer(connectionString));

//  إضافة وتكوين خدمة الجلسات (Session)
//  أضف هذه الأسطر لتفعيل خدمات الـ Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // مدة الجلسة
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 1. إضافة خدمات المصادقة باستخدام الكوكيز
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // تحديد مسار صفحة الدخول
        options.LogoutPath = "/Account/Logout";     // مسار تسجيل الخروج (اختياري لكن جيد)
        options.AccessDeniedPath = "/Account/AccessDenied"; // <<< أضف هذا السطر
        options.ExpireTimeSpan = TimeSpan.FromDays(30); // مدة صلاحية الكوكي
        options.SlidingExpiration = true;
    });

// أضف هذه الخدمة لتتمكن من الوصول لـ HttpContext في الـ Views
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles(); // هذا السطر ضروري لخدمة الملفات من wwwroot

app.UseRouting();
app.UseAuthentication(); // <<< الخطوة 1: تعرف على المستخدم أولاً
app.UseAuthorization();  // <<< الخطوة 2: تحقق من صلاحياته

// تفعيل استخدام المصادقة والصلاحيات (يجب أن يكونا بهذا الترتيب)
app.UseSession(); // <<< الخطوة 3: جهّز نظام الجلسة

// الآن، نخبر التطبيق بأن يستخدم الـ Middleware الجديد
// <<< الخطوة 4: الآن فقط، قم بتشغيل الـ Middleware الخاص بنا
app.UseMiddleware<SessionRefreshMiddleware>();

//app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");/*.WithStaticAssets()*/


app.Run();
