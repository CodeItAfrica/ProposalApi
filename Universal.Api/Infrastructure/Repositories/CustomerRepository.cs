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
        public Task<InsuredClients> CustomerSelectThisAsync(string appId, string customerId, string password)
        {
            return _db.InsuredClients.FirstOrDefaultAsync(x => 
                                  (x.InsuredID == customerId || x.Email == customerId)
                                //&& x.ApiPassword == password
                                && x.SubmittedBy == $"{SUBMITTED_BY}/{appId}");
        }

        public async Task<List<InsuredClients>> GetAllCustomersAsync()
        {
            return await _db.InsuredClients.ToListAsync();
        }
        public Task<InsuredClients> CustomerSelectThisAsync(string email, string mobile)
        {
            var query = _db.InsuredClients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
                return query.FirstOrDefaultAsync(x => x.Email == email);

            if (!string.IsNullOrWhiteSpace(mobile))
                return query.FirstOrDefaultAsync(x => x.MobilePhone == mobile || 
                                                      x.Landphone   == mobile);

            throw new ArgumentException("Please enter a valid email or phone number");
        }

        public async Task<int> GetTotalInsuredCountAsync()
        {
            return await _db.InsuredClients.CountAsync();
        }

        public Task<InsuredClients> CustomerSelectThisAsync(string customerId)
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

        public async Task<InsuredClients> CustomerGetOrAddAsync(Policiesdto newpolicydto)
        {
            if (!string.IsNullOrWhiteSpace(newpolicydto.CustomerID))
            {
                var insured = await CustomerSelectThisAsync(newpolicydto.CustomerID);

                if (insured != null)
                    return insured;
            }

            if (newpolicydto.Insured != null)
            {
                var newInsured = await CustomerCreate(newpolicydto.Insured);
                return new InsuredClients
                {
                    InsuredID = newInsured.InsuredID,
                    Address = newInsured.Address,
                    Email = newInsured.Email,
                    FullName = newInsured.FullName,
                    SurName = newInsured.SurName,
                    MobilePhone = newInsured.MobilePhone,
                    Landphone = newInsured.Landphone,
                    NationalID = newInsured.NationalID,
                    ClientType = newInsured.ClientType,
                    OtherNames = newInsured.OtherNames,
                    Remarks = newInsured.Remarks,
                    SubmittedBy = newInsured.SubmittedBy,
                    SubmittedOn = newInsured.SubmittedOn,
                    Active = newInsured.Active,
                    Deleted = newInsured.Deleted
                };
            }

            throw new InvalidOperationException("Either you specify CustomerID for existing customer, " +
                "or you create a new customer by populating the Insured object");
        }

        public async Task<InsuredClients> CustomerMasterGetOrAddAsync(PolicyModel newpolicydto)
        {
            if (!string.IsNullOrWhiteSpace(newpolicydto.CustomerID))
            {
                var insured = await CustomerSelectThisAsync(newpolicydto.CustomerID);

                if (insured != null)
                    return insured;
            }

            if (newpolicydto.Insured != null)
            {
                var newInsured = await CustomerCreateMaster(newpolicydto.Insured);

                // Fix: Map InsuredClient to InsuredClients
                return new InsuredClients
                {
                    InsuredID = newInsured.InsuredID,
                    Address = newInsured.Address,
                    Email = newInsured.Email,
                    FullName = newInsured.FullName,
                    SurName = newInsured.SurName,
                    MobilePhone = newInsured.MobilePhone,
                    Landphone = newInsured.Landphone,
                    NationalID = newInsured.NationalID,
                    ClientType = newInsured.ClientType,
                    OtherNames = newInsured.OtherNames,
                    Remarks = newInsured.Remarks,
                    SubmittedBy = newInsured.SubmittedBy,
                    SubmittedOn = newInsured.SubmittedOn,
                    Active = newInsured.Active,
                    Deleted = newInsured.Deleted
                };
            }

            throw new InvalidOperationException("Either you specify CustomerID for existing customer, " +
                "or you create a new customer by populating the Insured object");
        }

        public async Task<InsuredClients> CustomerCreateMaster(CreateNewCustomerRequestMaster newCustomerDto)
        {
            int incNumber = 0;
            incNumber++;

            string nyNumber = "S" + incNumber.ToString("00");

            ////check for duplicate
            //var duplicate = await CustomerSelectThisAsync(newCustomerDto.Email, newCustomerDto.PhoneLine1);

            //if (duplicate != null)
            //    throw new ArgumentException("Customer already exists with this email or phone number");

            var newInsured = new InsuredClients
            {
                InsuredID = GetNextAutoNumber("INSURED", "01") ?? "",

                Address = newCustomerDto.Address ?? "",
                Email = newCustomerDto.Email ?? "",
                FullName = newCustomerDto.FullName ?? "",
                SurName = newCustomerDto.Surname ?? "",
                MobilePhone = newCustomerDto.MobilePhone ?? "",
                Landphone = newCustomerDto.LandPhone ?? "",
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

        public async Task<InsuredClients> CustomerCreate(CreateNewCustomerRequest newCustomerDto)
        {
            int incNumber = 0;
            incNumber++;

            string nyNumber = "S" + incNumber.ToString("00");

            ////check for duplicate
            //var duplicate = await CustomerSelectThisAsync(newCustomerDto.Email, newCustomerDto.PhoneLine1);

            //if (duplicate != null)
            //    throw new ArgumentException("Customer already exists with this email or phone number");

            var newInsured = new InsuredClients // Changed from InsuredClient to InsuredClients
            {
                InsuredID = GetNextAutoNumber("INSURED", "01") ?? "",

                Address = newCustomerDto.Address ?? "",
                Email = newCustomerDto.Email ?? "",
                FullName = newCustomerDto.FirstName ?? "",
                SurName = newCustomerDto.LastName ?? "", // Changed from Surname to SurName
                MobilePhone = newCustomerDto.PhoneLine1 ?? "",
                Landphone = newCustomerDto.PhoneLine2 ?? "", // Changed from LandPhone to Landphone
                OtherNames = newCustomerDto.OtherName ?? "",

                Remarks = newCustomerDto.KycNumber ?? "",

                SubmittedBy = $"{SUBMITTED_BY}/{_authContext.User.AppId}" ?? "",
                SubmittedOn = DateTime.Now,
                Active = 1,
                Deleted = 0,
            };

            _db.InsuredClients.Add(newInsured); // No change here, as the type now matches
            return newInsured;
        }

        public Task<List<InsuredClients>> CustomerSelectAsync(FilterPaging filter)
        {
            if (filter == null)
                filter = new FilterPaging();

            var query = _db.InsuredClients.AsQueryable();

            if (_authContext.User is AppUser u)
                query = query.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{u.AppId}");

            //else if (_authContext.User is AgentUser a)
            //    query = query.Where(x => x.PartyID == a.PartyId);

            foreach (string item in filter.SearchTextItems)
                query = query.Where(x => x.SurName.Contains(item) || x.FullName.Contains(item)).AsQueryable();

            return query.OrderBy(x => x.SurName)
                        //.Skip(filter.SkipCount)
                        .Take(filter.PageSize)
                        .ToListAsync();
        }
    }
}
