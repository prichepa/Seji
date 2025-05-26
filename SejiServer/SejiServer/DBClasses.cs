namespace SejiServer
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public byte[] Avatar { get; set; }
        public string avatarExtension { get; set; }
        public string? ColorBrush { get; set; }
    }

    public class Chat
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsGroup { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public byte[] MessageContent { get; set; }
        public byte[] Extension { get; set; }
        public DateTime SentTime { get; set; }
    }

    public class ChatUser
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int UserId { get; set; }
    }
}
