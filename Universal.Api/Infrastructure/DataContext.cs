using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Domain;
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

            modelBuilder.Entity<Agents>()
                .HasMany(ir => ir.Attachments)
                .WithOne(a => a.Agents)
                .HasForeignKey(a => a.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PolicyAutoNumber>().HasKey(p => new { p.NumType, p.RiskID, p.BranchID, p.CompanyID });
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
        public DbSet<ApiUser> OpenApiUsers { get; set; }
        public DbSet<AutoNumber> AutoNumbers { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<SubRisk> SubRisks { get; set; }
        public DbSet<Policy> Policies { get; set; }
        public DbSet<Party> Parties { get; set; }
        public DbSet<Claim> ClaimsReserved { get; set; }
        public DbSet<DNCNNote> DNCNNotes { get; set; }
        public DbSet<PolicyDetail> PolicyDetails { get; set; }
        public DbSet<GrpMember> GrpMembers { get; set; }

        public DbSet<InsuredClient> InsuredClients { get; set; }
        public DbSet<PolicyMaster> PolicyMaster { get; set; }
        public DbSet<Agents> Agents { get; set; }
        public  DbSet<AgentAttachment> AgentAttachments { get; set; }


    }
}
