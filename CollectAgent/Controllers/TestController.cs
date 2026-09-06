// Controllers/TestController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize] // محمي بالكامل
public class TestController : Controller
{
    public IActionResult Index()
    {
        // هذا الكود لا يجب أن يتم الوصول إليه أبداً من قبل مستخدم غير مسجل
        return Content("If you can see this, [Authorize] is NOT working.");
    }
}