using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data;
using MySql;
using MySql.Data.MySqlClient;
using Microsoft.EntityFrameworkCore;

namespace DataModule.Model
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        { }

        public DataContext()
        {
            
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)=>
            optionsBuilder.UseMySql("Server=localhost;Port=3306;Database=nnhuman.cloudelevator;uid=root;pwd=123456;SslMode=none;");
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HotelElevator>().HasKey(t => new { t.HotelId, t.ElevatorId });
            modelBuilder.Entity<HotelRobot>().HasKey(t => new { t.HotelId, t.UniqueRobotSN });

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Hotel> Hotels { get; set; }
        public DbSet<HotelElevator> HotelElevators { get; set; }
        public DbSet<HotelRobot> HotelRobots { get; set; }
        public DbSet<RobotCompany> RobotCompanys { get; set; }
        public DbSet<RobotMap> RobotMaps { get; set; }
        public DbSet<ElevatorIdModule> ElevatorIdMudules { get; set; }
        public DbSet<ElevatorCompany> ElevatorCompanys { get; set; }
    }
}
