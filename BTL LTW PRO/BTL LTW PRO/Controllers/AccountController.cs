using BTL_LTW_PRO.Data;
using BTL_LTW_PRO.Models;
using BTL_LTW_PRO.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_LTW_PRO.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }


        // GET: Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: Register
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "model state invalid");
                return View(model);
            }

          
            // Kiểm tra trùng email
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(string.Empty, "Email đã tồn tại.");
                return View(model);
            }

            var user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                PasswordHash = HashPassword(model.Password),
                RoleID = model.RoleID,
            
            };

         

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }

        // GET: Login
       
        public IActionResult Login()
        {
            return View();
        }

        // POST: Login
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            // Kiểm tra nếu tài khoản đang bị khóa
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now)
            {
                var remainingTime = user.LockoutEnd.Value - DateTime.Now;
                ModelState.AddModelError(string.Empty, $"Tài khoản bị khóa. Vui lòng thử lại sau {remainingTime.Minutes} phút {remainingTime.Seconds} giây.");
                return View(model);
            }else if(user.LockoutEnd.HasValue && user.LockoutEnd.Value <= DateTime.Now)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
                await _context.SaveChangesAsync();
            }

            if (user.PasswordHash != HashPassword(model.Password))
            {
                user.FailedLoginAttempts++;

                // Nếu sai quá 3 lần thì khóa 30 phút
                if (user.FailedLoginAttempts >= 3)
                {
                    user.LockoutEnd = DateTime.Now.AddMinutes(30);
                    await _context.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty, "Bạn đã đăng nhập sai quá 3 lần. Tài khoản bị khóa trong 30 phút.");
                    return View(model);
                }

                var tmp = 3 - user.FailedLoginAttempts;

                await _context.SaveChangesAsync();
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng, bạn còn " + tmp + " lượt nhập!" );
                return View(model);
            }
               

            // Đăng nhập thành công, reset số lần thất bại
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _context.SaveChangesAsync();

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleID == user.RoleID);
            if (role == null)
            {
                return View(model);
            }

            HttpContext.Session.SetString("UserID", user.UserID.ToString());
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserRole", role.RoleName);

            if (user.RoleID == 1) // admin
            {
                return RedirectToAction("Privacy", "Home");
            }
            else if (user.RoleID == 2) // teacher
            {
                return RedirectToAction("Index", "Home");
            }
            else if (user.RoleID == 3) // student
            {
                return RedirectToAction("ShowCourse", "Student");
            }

            return View(model);
        }


        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        public IActionResult Logout()
        {

            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        


    }
}
