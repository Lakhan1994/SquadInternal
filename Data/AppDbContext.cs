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
        public DbSet<Asset> Assets => Set<Asset>();
        public DbSet<AssetHistory> AssetHistories => Set<AssetHistory>();

        public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();

        public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();

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


            // ================= ASSET CATEGORY =================
            modelBuilder.Entity<AssetCategory>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<AssetCategory>()
                .HasIndex(c => c.Name)
                .IsUnique();


            // ================= ASSET =================
            modelBuilder.Entity<Asset>()
                .HasKey(a => a.Id);

            modelBuilder.Entity<Asset>()
                .HasIndex(a => a.AssetCode)
                .IsUnique();

            modelBuilder.Entity<Asset>()
                .Property(a => a.PurchaseCost)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Asset>()
                .HasOne(a => a.AssetCategory)
                .WithMany(c => c.Assets)
                .HasForeignKey(a => a.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Asset>()
                .HasOne(a => a.CurrentEmployee)
                .WithMany()
                .HasForeignKey(a => a.CurrentEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);


            // ================= ASSET ASSIGNMENT =================
            modelBuilder.Entity<AssetAssignment>()
                .HasKey(a => a.Id);

            modelBuilder.Entity<AssetAssignment>()
                .HasOne(a => a.Asset)
                .WithMany(a => a.Assignments)
                .HasForeignKey(a => a.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AssetAssignment>()
                .HasOne(a => a.Employee)
                .WithMany()
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);


            // ================= ASSET HISTORY =================
            modelBuilder.Entity<AssetHistory>()
                .HasKey(h => h.Id);

            modelBuilder.Entity<AssetHistory>()
                .HasOne(h => h.Asset)
                .WithMany(a => a.History)
                .HasForeignKey(h => h.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
