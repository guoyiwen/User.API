using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using User.API.Models;

namespace User.API.Data
{
    public class UserContext : DbContext
    {
        public UserContext(DbContextOptions<UserContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .ConfigureWarnings(warnnings => warnnings.Log(CoreEventId.ProviderBaseId));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>()
                .ToTable("Users")
                .HasKey(u => u.Id);

            modelBuilder.Entity<UserProperty>()
                .Property(u => u.Value).HasMaxLength(100);
            modelBuilder.Entity<UserProperty>()
                .Property(u => u.Key).HasMaxLength(100);
            modelBuilder.Entity<UserProperty>()
                .ToTable("UserPropertys")
                .HasKey(u => new { u.Key, u.AppUserId, u.Value });

            modelBuilder.Entity<UserTag>().Property(u => u.Tag).HasMaxLength(100);
            modelBuilder.Entity<UserTag>()
                .ToTable("UserTags")
                .HasKey(u => new { u.UserId, u.Tag });



            modelBuilder.Entity<BPFile>()
                .ToTable("UserBPFile")
                .HasKey(u => new { u.Id });


            base.OnModelCreating(modelBuilder);

        }

        public DbSet<AppUser> Users { get; set; }

        public DbSet<UserProperty> UserProperties { get; set; }

        public DbSet<UserTag> UserTags { get; set; }



    }
}
