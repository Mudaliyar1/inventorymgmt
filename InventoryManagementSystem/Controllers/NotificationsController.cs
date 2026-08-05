using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _notificationRepository;

        public NotificationsController(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            var notifications = await _notificationRepository.GetUnreadNotificationsAsync();
            return Ok(notifications);
        }

        [HttpPost("mark-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _notificationRepository.MarkAllAsReadAsync();
            return Ok(new { success = true });
        }
    }
}
