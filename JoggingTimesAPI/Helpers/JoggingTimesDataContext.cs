﻿using JoggingTimesAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace JoggingTimesAPI.Helpers
{
    public class JoggingTimesDataContext : DbContext
    {
        public JoggingTimesDataContext(DbContextOptions<JoggingTimesDataContext> options)
            : base(options)
        {

        }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<JoggingTimeLog> JoggingTimeLogs { get; set; }
    }
}
