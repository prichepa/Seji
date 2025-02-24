using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace WpfWithLocalServer
{
    public class MyDBContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Chat> Chats => Set<Chat>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<ChatUser> ChatUsers => Set<ChatUser>();

        string connectionString;

        public MyDBContext(string connectionString)
        {
            this.connectionString = connectionString;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
    }
}
