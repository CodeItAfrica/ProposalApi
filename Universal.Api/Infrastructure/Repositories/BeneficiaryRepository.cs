using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    public class BeneficiaryRepository
    {
        private readonly DataContext _db;

        public BeneficiaryRepository(DataContext context)
        {
            _db = context;
        }

        // Add pagination support with AsNoTracking for better performance
        public async Task<(IEnumerable<Beneficiary> Items, int TotalCount)> GetAllAsync(
            int pageNumber = 1,
            int pageSize = 50)
        {
            var query = _db.Beneficiaries
                .Where(b => b.Deleted == null || b.Deleted == 0)
                .AsNoTracking();

            var totalCount = await query.CountAsync();

            // Load all fields except Photo to avoid NULL issues with projection
            var items = await query
                .OrderByDescending(b => b.TransID)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Clear Photo data after loading to reduce memory
            foreach (var item in items)
            {
                item.Photo = null;
            }

            return (items, totalCount);
        }

        public async Task<Beneficiary> GetByIdAsync(int id)
        {
            return await _db.Beneficiaries
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.TransID == id && b.Deleted == 0);
        }

        public async Task<IEnumerable<Beneficiary>> GetByPolicyNoAsync(string policyNo)
        {
            var items = await _db.Beneficiaries
                .Where(b => b.PolicyNo == policyNo && b.Deleted == 0)
                .OrderByDescending(b => b.TransID)
                .AsNoTracking()
                .ToListAsync();

            // Clear Photo data to reduce memory
            foreach (var item in items)
            {
                item.Photo = null;
            }

            return items;
        }

        public async Task<IEnumerable<Beneficiary>> GetByProposalNoAsync(string proposalNo)
        {
            var items = await _db.Beneficiaries
                .Where(b => b.ProposalNo == proposalNo && b.Deleted == 0)
                .OrderByDescending(b => b.TransID)
                .AsNoTracking()
                .ToListAsync();

            // Clear Photo data to reduce memory
            foreach (var item in items)
            {
                item.Photo = null;
            }

            return items;
        }

        public async Task<Beneficiary> CreateAsync(Beneficiary beneficiary)
        {
            beneficiary.SubmittedOn = DateTime.Now;
            _db.Beneficiaries.Add(beneficiary);
            await _db.SaveChangesAsync();
            return beneficiary;
        }

        public async Task<Beneficiary> UpdateAsync(Beneficiary beneficiary)
        {
            beneficiary.ModifiedOn = DateTime.Now;
            _db.Entry(beneficiary).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return beneficiary;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var beneficiary = await _db.Beneficiaries.FindAsync(id);
            if (beneficiary == null)
                return false;

            beneficiary.Deleted = 1;
            beneficiary.ModifiedOn = DateTime.Now;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PermanentDeleteAsync(int id)
        {
            var beneficiary = await _db.Beneficiaries.FindAsync(id);
            if (beneficiary == null)
                return false;

            _db.Beneficiaries.Remove(beneficiary);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _db.Beneficiaries.AnyAsync(b => b.TransID == id);
        }
    }
}