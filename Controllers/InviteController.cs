using Microsoft.AspNetCore.Mvc;
using ChessServer.Services;

namespace ChessServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InviteController : ControllerBase
    {
        private readonly GameManager _gameManager;
        
        public InviteController(GameManager gameManager)
        {
            _gameManager = gameManager;
        }
        
        [HttpPost("telegram")]
        public IActionResult GenerateTelegramInvite([FromBody] TelegramInviteRequest request)
        {
            // در اینجا می‌توانید با Telegram Bot API ارتباط برقرار کنید
            // فعلاً یک کد دعوت ساده برمی‌گردانیم
            
            var room = _gameManager.CreateGame(request.GameName, true);
            
            var inviteLink = $"https://t.me/your_bot?start=chess_{room.InviteCode}";
            
            return Ok(new
            {
                InviteLink = inviteLink,
                InviteCode = room.InviteCode,
                RoomId = room.RoomId,
                ExpiresIn = "24 hours"
            });
        }
    }
    
    public class TelegramInviteRequest
    {
        public string GameName { get; set; } = "Chess Game";
        public string? OpponentTelegramId { get; set; }
    }
}