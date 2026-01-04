using Microsoft.AspNetCore.SignalR;

namespace ChessServer.Hubs
{
    public class GameHub : Hub
    {
        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }

        public async Task SendMove(string roomId, object move)
        {
            await Clients.OthersInGroup(roomId)
                .SendAsync("ReceiveMove", move);
        }
    }
}
