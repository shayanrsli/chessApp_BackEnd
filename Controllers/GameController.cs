using Microsoft.AspNetCore.Mvc;
using ChessServer.Services;
using ChessServer.Models;

namespace ChessServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        private readonly GameManager _gameManager;
        
        public GameController(GameManager gameManager)
        {
            _gameManager = gameManager;
        }
        
        [HttpGet("public")]
        public IActionResult GetPublicGames()
        {
            var publicGames = _gameManager.GetAllGames()
                .Where(g => !g.IsPrivate && !g.IsFull)
                .Select(g => new
                {
                    g.RoomId,
                    g.Name,
                    WhitePlayer = g.WhitePlayer?.Username,
                    BlackPlayer = g.BlackPlayer?.Username,
                    g.Status,
                    PlayerCount = (g.WhitePlayer != null ? 1 : 0) + (g.BlackPlayer != null ? 1 : 0)
                });
            
            return Ok(publicGames);
        }
        
        [HttpPost("create")]
        public IActionResult CreateGame([FromBody] CreateGameRequest request)
        {
            var room = _gameManager.CreateGame(request.GameName, request.IsPrivate);
            
            return Ok(new
            {
                RoomId = room.RoomId,
                InviteCode = room.InviteCode,
                Room = new
                {
                    room.RoomId,
                    room.Name,
                    room.Status,
                    room.IsPrivate,
                    WhitePlayer = room.WhitePlayer?.Username
                }
            });
        }
        
        [HttpGet("invite/{inviteCode}")]
        public IActionResult GetGameByInviteCode(string inviteCode)
        {
            // از متد جدید استفاده می‌کنیم
            var room = _gameManager.GetGameByInviteCode(inviteCode);
                
            if (room == null) return NotFound("Game not found");
            
            return Ok(new
            {
                RoomId = room.RoomId,
                RoomName = room.Name,
                Players = new
                {
                    White = room.WhitePlayer?.Username,
                    Black = room.BlackPlayer?.Username
                },
                Status = room.Status.ToString(),
                IsFull = room.IsFull,
                IsPrivate = room.IsPrivate
            });
        }
        
        // اضافه کردن endpoint برای گرفتن اطلاعات یک بازی خاص
        [HttpGet("{roomId}")]
        public IActionResult GetGame(string roomId)
        {
            var room = _gameManager.GetGame(roomId);
            if (room == null) return NotFound("Game not found");
            
            return Ok(new
            {
                room.RoomId,
                room.Name,
                room.Status,
                room.IsPrivate,
                room.CurrentFen,
                WhitePlayer = room.WhitePlayer != null ? new
                {
                    room.WhitePlayer.Username,
                    room.WhitePlayer.ConnectionId
                } : null,
                BlackPlayer = room.BlackPlayer != null ? new
                {
                    room.BlackPlayer.Username,
                    room.BlackPlayer.ConnectionId
                } : null,
                MoveCount = room.Moves.Count,
                CanJoin = !room.IsFull && room.Status == ChessServer.Models.Enums.GameStatus.WaitingForPlayer
            });
        }
    }
    
    public class CreateGameRequest
    {
        public string GameName { get; set; } = "Chess Game";
        public bool IsPrivate { get; set; } = true;
    }
}