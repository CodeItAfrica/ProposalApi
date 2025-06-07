using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using Castle.Core.Resource;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {
        public Task<InsuredClient> CustomerSelectThisAsync(string appId, string customerId, string password)
        {
            return _db.InsuredClients.FirstOrDefaultAsync(x => 
                                  (x.InsuredID == customerId || x.Email == customerId)
                                //&& x.ApiPassword == password
                                && x.SubmittedBy == $"{SUBMITTED_BY}/{appId}");
        }

        public async Task<List<InsuredClient>> GetAllCustomersAsync()
        {
            return await _db.InsuredClients.ToListAsync();
        }
        public Task<InsuredClient> CustomerSelectThisAsync(string email, string mobile)
        {
            var query = _db.InsuredClients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
                return query.FirstOrDefaultAsync(x => x.Email == email);

            if (!string.IsNullOrWhiteSpace(mobile))
                return query.FirstOrDefaultAsync(x => x.MobilePhone == mobile || 
                                                      x.LandPhone   == mobile);

            throw new ArgumentException("Please enter a valid email or phone number");
        }

        public async Task<int> GetTotalInsuredCountAsync()
        {
            return await _db.InsuredClients.CountAsync();
        }

        public Task<InsuredClient> CustomerSelectThisAsync(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
                throw new ArgumentNullException(nameof(customerId));

            if (customerId.IsValidEmail())
                return _db.InsuredClients.FirstOrDefaultAsync(x => x.Email == customerId);

            if (customerId.IsNumeric())
                return _db.InsuredClients.FirstOrDefaultAsync(x => x.MobilePhone == customerId 
                                                                || x.InsuredID   == customerId);

            return _db.InsuredClients.FirstOrDefaultAsync(x => x.InsuredID == customerId);
        }

        public async Task<InsuredClient> CustomerGetOrAddAsync(Policiesdto newpolicydto) 

        {
            if (!string.IsNullOrWhiteSpace(newpolicydto.CustomerID))
            {
                var insured = await CustomerSelectThisAsync(newpolicydto.CustomerID);

                if (insured != null)
                    return insured;
            }

            if (newpolicydto.Insured != null)
                return await CustomerCreate(newpolicydto.Insured);

            throw new InvalidOperationException("Either you specify CustomerID for existing customer, " +
                "or you create a new customer by populating the Insured object");
        }

        public async Task<InsuredClient> CustomerMasterGetOrAddAsync(PolicyModel newpolicydto)

        {
            if (!string.IsNullOrWhiteSpace(newpolicydto.CustomerID))
            {
                var insured = await CustomerSelectThisAsync(newpolicydto.CustomerID);

                if (insured != null)
                    return insured;
            }

            if (newpolicydto.Insured != null)
                return await CustomerCreateMaster(newpolicydto.Insured);

            throw new InvalidOperationException("Either you specify CustomerID for existing customer, " +
                "or you create a new customer by populating the Insured object");
        }

        public async Task<InsuredClient> CustomerCreateMaster(CreateNewCustomerRequestMaster newCustomerDto)
        {
            int incNumber = 0;
            incNumber++;

            string nyNumber = "S" + incNumber.ToString("00");

            ////check for duplicate
            //var duplicate = await CustomerSelectThisAsync(newCustomerDto.Email, newCustomerDto.PhoneLine1);

            //if (duplicate != null)
            //    throw new ArgumentException("Customer already exists with this email or phone number");

            var newInsured = new InsuredClient
            {
                InsuredID = GetNextAutoNumber("INSURED", "01") ?? "",

                Address = newCustomerDto.Address ?? "",
                Email = newCustomerDto.Email ?? "",
                FullName = newCustomerDto.FullName ?? "",
                Surname = newCustomerDto.Surname ?? "",
                MobilePhone = newCustomerDto.MobilePhone ?? "",
                LandPhone = newCustomerDto.LandPhone ?? "",
                NationalID = newCustomerDto.NationalID,
                ClientType = "INDIVIDUAL",
                //Occupation = newCustomerDto.Industry,
                OtherNames = newCustomerDto.OtherNames ?? "",

                Remarks = newCustomerDto.KycNumber ?? "",

                //SubmittedBy = $"{SUBMITTED_BY}/{_authContext.User.AppId}" ?? "",
                SubmittedOn = DateTime.Now,
                Active = 1,
                Deleted = 0,

                //ApiId = newCustomerDto.Email,
                //ApiPassword = newCustomerDto.Password,
                //ApiStatus = "ENABLED", 
            };

            _db.InsuredClients.Add(newInsured);
            return newInsured;
        }

        public async Task<InsuredClient> CustomerCreate(CreateNewCustomerRequest newCustomerDto)
        {
            int incNumber = 0;
            incNumber++;

            string nyNumber = "S" + incNumber.ToString("00");
      
            ////check for duplicate
            //var duplicate = await CustomerSelectThisAsync(newCustomerDto.Email, newCustomerDto.PhoneLine1);

            //if (duplicate != null)
            //    throw new ArgumentException("Customer already exists with this email or phone number");

            var newInsured = new InsuredClient
            {
                InsuredID = GetNextAutoNumber("INSURED", "01") ?? "",

                Address = newCustomerDto.Address ?? "",
                Email = newCustomerDto.Email ?? "",
                FullName = newCustomerDto.FirstName ?? "",
                Surname = newCustomerDto.LastName ?? "",
                MobilePhone = newCustomerDto.PhoneLine1 ?? "",
                LandPhone = newCustomerDto.PhoneLine2 ?? "",
                //Occupation = newCustomerDto.Industry,
                OtherNames = newCustomerDto.OtherName ?? "",  

                Remarks = newCustomerDto.KycNumber ?? "",

                SubmittedBy = $"{SUBMITTED_BY}/{_authContext.User.AppId}" ?? "",
                SubmittedOn = DateTime.Now ,
                Active = 1 ,
                Deleted = 0,

                //ApiId = newCustomerDto.Email,
                //ApiPassword = newCustomerDto.Password,
                //ApiStatus = "ENABLED", 
            };

            _db.InsuredClients.Add(newInsured);
            return newInsured;
        }

        public Task<List<InsuredClient>> CustomerSelectAsync(FilterPaging filter)
        {
            if (filter == null)
                filter = new FilterPaging();

            var query = _db.InsuredClients.AsQueryable();

            if (_authContext.User is AppUser u)
                query = query.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{u.AppId}");

            //else if (_authContext.User is AgentUser a)
            //    query = query.Where(x => x.PartyID == a.PartyId);

            foreach (string item in filter.SearchTextItems)
                query = query.Where(x => x.Surname.Contains(item) || x.FullName.Contains(item)).AsQueryable();

            return query.OrderBy(x => x.Surname)
                        //.Skip(filter.SkipCount)
                        .Take(filter.PageSize)
                        .ToListAsync();
        }
    }
}
