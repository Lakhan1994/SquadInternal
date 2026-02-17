using Microsoft.EntityFrameworkCore;
using SquadInternal.Models;

namespace SquadInternal.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<EmployeeEducation> EmployeeEducations => Set<EmployeeEducation>();
        public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
        public DbSet<SquadHoliday> SquadHolidays => Set<SquadHoliday>();
        public DbSet<EmployeeLeave> EmployeeLeaves => Set<EmployeeLeave>();


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ================= USER =================
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // ================= ROLES SEED =================
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Employee" },
                new Role { Id = 3, Name = "HR" }
            );

            // ================= EMPLOYEE =================
            modelBuilder.Entity<Employee>()
                .Property(e => e.Salary)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Employee>()
                .HasQueryFilter(e => !e.IsDeleted);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.AddedByUser)
                .WithMany()
                .HasForeignKey(e => e.AddedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= EMPLOYEE EDUCATION =================
            modelBuilder.Entity<EmployeeEducation>()
                .HasOne(ed => ed.Employee)
                .WithMany(e => e.Educations)
                .HasForeignKey(ed => ed.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= EMPLOYEE DOCUMENT =================
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.Employee)
                .WithMany()
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= SQUAD HOLIDAY =================
            modelBuilder.Entity<SquadHoliday>()
                .HasKey(h => h.Id);


            modelBuilder.Entity<Employee>()
     .HasOne(e => e.ReportingToUser)
     .WithMany()
     .HasForeignKey(e => e.ReportingToUserId)
     .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
