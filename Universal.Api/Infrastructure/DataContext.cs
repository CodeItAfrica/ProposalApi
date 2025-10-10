using DocumentFormat.OpenXml.Spreadsheet;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection.Emit;

namespace GibsLifesMicroWebApi.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Agents>()
        .HasKey(a => a.INT);

            modelBuilder.Entity<Agents>()
                .HasAlternateKey(a => a.AgentID); // declare AgentID as alternate key

            modelBuilder.Entity<AgentAttachment>()
     .HasOne<Agents>()
     .WithMany(a => a.Attachments)
     .HasForeignKey(a => a.AgentId)
     .HasPrincipalKey(a => a.AgentID); // or .AgentID if you’re using that as alternate key

            //Map FK: QuoteMember.QuotationNo->QuoteMaster.QuoteID(not enforced by FK constraint if legacy DB; use HasNoKey if needed)
                modelBuilder.Entity<QuoteMember>()
                    .HasIndex(m => m.QuotationNo);

            //        modelBuilder.Entity<QuoteMember>()
            //.HasOne(qm => qm.QuotesMaster)
            //.WithMany(m => m.QuoteMembers)
            //.HasForeignKey(qm => qm.QuotationNo) // actual DB column
            //.HasPrincipalKey(m => m.QuoteNo);    // PK in QuotesMaster
            modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserID, ur.RoleID });
            modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleID, rp.PermissionID });
            modelBuilder.Entity<RolePermission>()
     .HasOne(rp => rp.Role)
     .WithMany(r => r.RolePermissions)
     .HasForeignKey(rp => rp.RoleID);
            modelBuilder.Entity<UserPermission>()
    .HasOne(up => up.User)
    .WithMany(u => u.UserPermissions)
    .HasForeignKey(up => up.UserID);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionID);
            modelBuilder.Entity<UserPermission>().HasKey(up => new { up.UserID, up.PermissionID });
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataContext).Assembly);

            modelBuilder.Entity<Agents>()
                .HasMany(ir => ir.Attachments)
                .WithOne(a => a.Agents)
                .HasForeignKey(a => a.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PolicyAutoNumber>().HasKey(p => new { p.NumType, p.RiskID, p.BranchID, p.CompanyID });


            //modelBuilder.Entity<QuotesMaster>()
            //   .HasMany(q => q.Members)
            //   .WithOne()
            //   .HasForeignKey(m => m.QuotationNo)
            //   .HasPrincipalKey(q => q.QuoteNo);

            //modelBuilder.Entity<QuoteMember>()
            //    .Property(m => m.BasicSalary)
            //    .HasPrecision(18, 2);

            //modelBuilder.Entity<QuoteMember>()
            //    .Property(m => m.TEmolument)
            //    .HasPrecision(18, 2);

            //modelBuilder.Entity<QuotesMaster>()
            //    .Property(m => m.TotalPremium)
            //    .HasPrecision(18, 2);

            modelBuilder.Entity<QuotesMaster>(entity =>
            {
                entity.ToTable("QuotesMaster", "dbo");
                entity.HasKey(e => e.QuoteID);
                entity.Property(e => e.QuoteID).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<QuoteMember>(entity =>
            {
                entity.ToTable("QuoteMembers", "dbo");
                entity.HasKey(e => e.MemberID);
                entity.Property(e => e.MemberID).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<UndPremRate>(entity =>
            {
                entity.ToTable("UndPremRates", "dbo");
                entity.HasKey(e => e.RateID);
            });
            //builder.Entity<Agents>()
            //    .HasMany(ir => ir.Attachments)
            //    .WithOne() // Removed the reference to 'Agent' as it does not exist in 'AgentAttachment'  
            //    .HasForeignKey(a => a.AgentId)
            //    .OnDelete(DeleteBehavior.Cascade);
            //        modelBuilder.Entity<AgentAttachmentAgent>()
            //.HasKey(x => new { x.AgentId, x.AgentAttachmentId });

            //        modelBuilder.Entity<AgentAttachmentAgent>()
            //            .HasOne(aa => aa.Agent)
            //            .WithMany(a => a.AgentAttachmentAgents)
            //            .HasForeignKey(aa => aa.AgentId);

            //        modelBuilder.Entity<AgentAttachmentAgent>()
            //            .HasOne(aa => aa.AgentAttachment)
            //            .WithMany(aa => aa.AgentAttachmentAgents)
            //            .HasForeignKey(aa => aa.AgentAttachmentId);

        }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Document> Documents { get; set; }
        //public DbSet<ApiUser> OpenApiUsers { get; set; }
        // Security
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<AutoNumber> AutoNumbers { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<SubRisk> SubRisks { get; set; }
        public DbSet<Policy> Policies { get; set; }
        public DbSet<Party> Parties { get; set; }
        public DbSet<Claim> ClaimsReserved { get; set; }
        public DbSet<DNCNNote> DNCNNotes { get; set; }
        public DbSet<PolicyDetail> PolicyDetails { get; set; }
        public DbSet<GrpMember> GrpMembers { get; set; }

        public DbSet<InsuredClients> InsuredClients { get; set; }
        public DbSet<PolicyMaster> PolicyMaster { get; set; }
        public DbSet<Agents> Agents { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<ProductSettings> ProductSettings { get; set; }
        public DbSet<QuotesMaster> QuotesMaster { get; set; }
        public DbSet<QuoteMember> QuoteMembers { get; set; }
        public DbSet<InterestRate> InterestRate { get; set; }
        public DbSet<AttachDoc> AttachDocs { get; set; }
        public DbSet<UndPremRate> UndPremRates { get; set; }


        public DbSet<AgentAttachment> AgentAttachments { get; set; }


    }
}
