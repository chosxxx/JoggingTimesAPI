﻿using Microsoft.EntityFrameworkCore;

namespace JoggingTimesAPI.Helpers
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
    }
}