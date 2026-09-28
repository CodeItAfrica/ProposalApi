using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Models.Customer;
using GibsLifesMicroWebApi.Domain;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    // Same partial class / same public method signatures as before, with one
    // exception noted inline (FindDuplicateAsync gains two OPTIONAL trailing
    // params, so existing 2-arg callers keep compiling unchanged).
    public partial class InsuredRepository
    {
        private readonly DataContext _db;
        private readonly AuthContext _authContext;
        private readonly Repository _repository;

        public InsuredRepository(DataContext db, AuthContext authContext, Repository repository)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _authContext = authContext ?? throw new ArgumentNullException(nameof(authContext));
            _repository = repository;
        }

        // -----------------------------------------------------------
        // Assured-code get-or-create (used by the invoice/traditional
        // quote flows, not the Customers controller directly). Unchanged
        // from the previous pass - out of scope here (different DTOs,
        // PolicyInvRequestDto/PolicyTradRequestDto, with no IsOrg concept
        // visible from this file). If that quote flow also needs corporate
        // support, it needs the same treatment applied separately.
        // -----------------------------------------------------------

        public Task<string> CreateOrGetAssuredCodeAsync(PolicyInvRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!string.IsNullOrWhiteSpace(request.AssuredCode))
                return Task.FromResult(request.AssuredCode);

            return CreateAssuredAsync(
                request.SurName, request.OtherNames, request.Title, request.Address,
                request.FullName, request.State, request.Landphone, request.MobilePhone,
                request.Email, request.NationalID, request.Occupation, request.Gender,
                request.MaritalStatus, request.Country, request.DateOfBirth);
        }

        public Task<string> CreateOrGetAssuredTradCodeAsync(PolicyTradRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!string.IsNullOrWhiteSpace(request.AssuredCode))
                return Task.FromResult(request.AssuredCode);

            return CreateAssuredAsync(
                request.SurName, request.OtherNames, request.Title, request.Address,
                request.FullName, request.State, request.Landphone, request.MobilePhone,
                request.Email, request.NationalID, request.Occupation, request.Gender,
                request.MaritalStatus, request.Country, request.DateOfBirth);
        }

        private async Task<string> CreateAssuredAsync(
            string surName, string otherNames, string title, string address,
            string fullName, string state, string landphone, string mobilePhone,
            string email, string nationalId, string occupation, string gender,
            string maritalStatus, string country, DateTime? dateOfBirth)
        {
            var insuredId = _repository.GetNextAutoNumber("INSURED", "01");

            if (string.IsNullOrWhiteSpace(insuredId))
                throw new InvalidOperationException("Failed to allocate a new INSURED number.");

            var insured = new InsuredClients
            {
                InsuredID = insuredId,
                SurName = surName,
                OtherNames = otherNames,
                Title = title,
                Address = address,
                FullName = fullName,
                State = state,
                Landphone = landphone,
                MobilePhone = mobilePhone,
                Email = email,
                NationalID = nationalId,
                Occupation = occupation,
                Sex = gender,
                MaritalStatus = maritalStatus,
                Country = country,
                DateofBirth = dateOfBirth,
                Age = CalculateAge(dateOfBirth),
                ClientType = ClientTypes.Individual,
                SubmittedBy = _authContext.User?.AppId ?? "system",
                SubmittedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                Active = 1,
                Deleted = 0
            };

            _db.InsuredClients.Add(insured);
            // NOTE (pre-existing, unchanged): this method commits
            // immediately, unlike CustomerCreate below (which leaves the
            // commit to the caller via Repository.SaveChangesAsync). That
            // split unit-of-work means this insert can't be rolled back as
            // part of a larger transaction the caller might be running.
            await _db.SaveChangesAsync();
            return insured.InsuredID;
        }

        private static decimal? CalculateAge(DateTime? dateOfBirth)
        {
            if (!dateOfBirth.HasValue) return null;
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Value.Year;
            if (today < dateOfBirth.Value.AddYears(age)) age--;
            return age;
        }

        // -----------------------------------------------------------
        // Lookups - unchanged from the previous pass.
        // -----------------------------------------------------------

        /// <summary>App-scoped lookup: matches by id-or-email AND SubmittedBy == "{submittedBy}/{appId}".</summary>
        public Task<InsuredClients> CustomerSelectThisAsync(string appId, string customerId, string password, object submittedBy)
        {
            return _db.InsuredClients.FirstOrDefaultAsync(x =>
                (x.InsuredID == customerId || x.Email == customerId)
                && x.SubmittedBy == $"{submittedBy}/{appId}");
        }

        /// <summary>Matches by exact email, or by mobile/landline phone. Throws if both are blank.</summary>
        public Task<InsuredClients> CustomerSelectThisAsync(string email, string mobile)
        {
            if (!string.IsNullOrWhiteSpace(email))
                return _db.InsuredClients.FirstOrDefaultAsync(x => x.Email == email);

            if (!string.IsNullOrWhiteSpace(mobile))
                return _db.InsuredClients.FirstOrDefaultAsync(x =>
                    x.MobilePhone == mobile || x.Landphone == mobile);

            throw new ArgumentException("Please enter a valid email or phone number");
        }

        /// <summary>
        /// Single-identifier lookup: matches by email if it looks like one,
        /// by mobile-or-id if it's all digits, otherwise by id. Works
        /// unchanged for corporate rows (NationalID holds the CAC number
        /// there; InsuredID lookup is identical either way).
        /// NOTE (pre-existing, not introduced by this pass): InsuredID is
        /// itself a numeric string, so the numeric branch can in principle
        /// match the wrong record if one customer's phone number equals
        /// another customer's InsuredID. Low probability but a real
        /// collision - worth an explicit lookup-type parameter eventually.
        /// </summary>
        public Task<InsuredClients> CustomerSelectThisAsync(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
                throw new ArgumentNullException(nameof(customerId));

            if (customerId.IsValidEmail())
                return _db.InsuredClients.FirstOrDefaultAsync(x => x.Email == customerId);

            if (customerId.IsNumeric())
                return _db.InsuredClients.FirstOrDefaultAsync(x =>
                    x.MobilePhone == customerId || x.InsuredID == customerId);

            return _db.InsuredClients.FirstOrDefaultAsync(x => x.InsuredID == customerId);
        }

        /// <summary>
        /// Used by CustomerCreate/CustomerCreateMaster to reject duplicates
        /// before insert.
        ///
        /// FIX (this pass): gained nationalId/clientType, both optional so
        /// existing 2-arg callers are unaffected. For a corporate create,
        /// the caller now passes the CAC number as nationalId plus
        /// ClientTypes.Corporate, so two corporates can't register the same
        /// registration number - scoped by clientType so an individual's NIN
        /// can never collide with an unrelated corporate's CAC number just
        /// because the digit strings happen to match.
        /// </summary>
        public Task<InsuredClients> FindDuplicateAsync(string email, string phone, string nationalId = null, string clientType = null)
        {
            return _db.InsuredClients.FirstOrDefaultAsync(x =>
                (!string.IsNullOrWhiteSpace(email) && x.Email == email) ||
                (!string.IsNullOrWhiteSpace(phone) && (x.MobilePhone == phone || x.Landphone == phone)) ||
                (!string.IsNullOrWhiteSpace(nationalId) && x.NationalID == nationalId
                    && (clientType == null || x.ClientType == clientType)));
        }

        public async Task<int> GetTotalInsuredCountAsync() => await _db.InsuredClients.CountAsync();

        public async Task<PagedResult<InsuredClients>> GetAllCustomersAsync(int pageNumber, int pageSize)
        {
            var query = _db.InsuredClients.AsQueryable();
            var totalRecords = await query.CountAsync();

            var customers = await query
                .OrderByDescending(c => c.SubmittedOn)
                .ThenByDescending(c => c.InsuredID)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<InsuredClients>(customers, pageNumber, pageSize, totalRecords);
        }

        public class PagedResult<T>
        {
            public List<T> Data { get; set; }
            public int PageNumber { get; set; }
            public int PageSize { get; set; }
            public int TotalRecords { get; set; }
            public int TotalPages { get; set; }
            public bool HasPreviousPage => PageNumber > 1;
            public bool HasNextPage => PageNumber < TotalPages;

            public PagedResult(List<T> data, int pageNumber, int pageSize, int totalRecords)
            {
                Data = data;
                PageNumber = pageNumber;
                PageSize = pageSize;
                TotalRecords = totalRecords;
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            }
        }

        /// <summary>Text search scoped to the caller's own submitted customers.</summary>
        public async Task<List<InsuredClients>> CustomerSelectAsync(FilterPaging filter)
        {
            filter ??= new FilterPaging();

            var query = _db.InsuredClients.AsQueryable();

            if (_authContext.User is AppUser u)
                query = query.Where(x => x.SubmittedBy == u.AppId);
            //else if (_authContext.User is AgentUser a)
            //    query = query.Where(x => x.PartyID == a.PartyId);
            // TODO (pre-existing, untouched): the AgentUser branch is still
            // commented out. An agent caller currently gets results scoped
            // only by SearchTextItems, not by their own party/portfolio.

            foreach (string item in filter.SearchTextItems)
                query = query.Where(x => x.SurName.Contains(item) || x.FullName.Contains(item));

            return await query
                .OrderBy(x => x.SurName)
                .Skip(filter.SkipCount)
                .Take(filter.PageSize)
                .ToListAsync();
        }

        // -----------------------------------------------------------
        // Create
        //
        // FIX (this pass): CustomerCreate and CustomerCreateMaster now both
        // funnel through one private helper. This is where the
        // individual/corporate field-reuse mapping lives (see the doc
        // comment on InsuredClients for the full table) - one place to get
        // it right instead of two copies that can silently drift apart, the
        // way they had (ClientType = "Individual" in one, "INDIVIDUAL" in
        // the other; IsOrg/OrgName/OrgRegNumber/OrgRegDate accepted by the
        // DTOs but written by neither).
        // -----------------------------------------------------------

        /// <summary>
        /// Builds, duplicate-checks and stages (Add, not SaveChanges - same
        /// as before, caller commits) a new InsuredClients row for either an
        /// individual or a corporate customer.
        /// </summary>
        private async Task<InsuredClients> BuildAndCheckDuplicateInsuredAsync(
            string surName, string otherNames, string fullName, string title,
            string address, string cityLGA, string state, string landphone, string mobilePhone,
            string email, string nationalId, string occupation, string gender,
            string maritalStatus, string country, DateTime? dateOfBirth,
            string kycNumber, PersonRequest nextOfKin,
            bool isCorporate, string orgName, string orgRegNumber, DateTime? orgRegDate)
        {
            // Corporate reuse mapping - see InsuredClients doc comment for
            // the full rationale. Validation on the DTOs (IValidatableObject)
            // already guarantees orgName/orgRegNumber are populated whenever
            // isCorporate is true, so the fallback to the individual-shaped
            // params here is just defensive, not the expected path.
            string effectiveFullName = isCorporate ? (orgName ?? fullName) : fullName;
            string effectiveNationalId = isCorporate ? (orgRegNumber ?? nationalId) : nationalId;
            DateTime? effectiveDob = isCorporate ? orgRegDate : dateOfBirth;
            string clientType = isCorporate ? ClientTypes.Corporate : ClientTypes.Individual;

            var duplicate = await FindDuplicateAsync(email, mobilePhone, effectiveNationalId, clientType);
            if (duplicate != null)
            {
                var descriptor = isCorporate
                    ? "registration number, email or phone number"
                    : "email or phone number";
                throw new InvalidOperationException(
                    $"A customer already exists with this {descriptor} (InsuredID: {duplicate.InsuredID}).");
            }

            var insuredId = _repository.GetNextAutoNumber("INSURED", "01");
            if (string.IsNullOrWhiteSpace(insuredId))
                throw new InvalidOperationException("Failed to allocate a new INSURED number.");

            var insured = new InsuredClients
            {
                InsuredID = insuredId,
                // Contact person - always populated regardless of type.
                SurName = surName ?? "",
                OtherNames = otherNames ?? "",
                Title = title,
                Sex = gender,
                MaritalStatus = maritalStatus,

                // Identity - org name/CAC/incorporation-date for corporate,
                // person's name/NIN/DOB for individual.
                FullName = effectiveFullName ?? "",
                NationalID = effectiveNationalId,
                DateofBirth = effectiveDob,
                Age = CalculateAge(effectiveDob),
                ClientType = clientType,

                Address = address ?? "",
                CityLGA = cityLGA,
                State = state,
                Landphone = landphone ?? "",
                MobilePhone = mobilePhone ?? "",
                Email = email ?? "",
                Occupation = occupation,
                Country = country,

                // Pre-existing hack, kept as-is: KycNumber rides in Remarks
                // since there's no dedicated column and this pass avoids a
                // migration.
                Remarks = kycNumber ?? "",

                SubmittedBy = _authContext.User?.AppId ?? "",
                SubmittedOn = DateTime.Now,
                Active = 1,
                Deleted = 0,
            };

            // NEW (this pass): next-of-kin now lands directly in the
            // NextOfKin* columns on this same row - no separate table, no
            // separate Add/commit to coordinate. Left null (all nine
            // columns) when nextOfKin wasn't submitted.
            if (nextOfKin != null)
            {
                insured.NextOfKinTitle = nextOfKin.Title;
                insured.NextOfKinFirstName = nextOfKin.FirstName;
                insured.NextOfKinLastName = nextOfKin.LastName;
                insured.NextOfKinOtherName = nextOfKin.OtherName;
                insured.NextOfKinGender = nextOfKin.Gender?.ToString();
                insured.NextOfKinEmail = nextOfKin.Email;
                insured.NextOfKinAddress = nextOfKin.Address;
                insured.NextOfKinPhoneLine1 = nextOfKin.PhoneLine1;
                insured.NextOfKinPhoneLine2 = nextOfKin.PhoneLine2;
            }

            _db.InsuredClients.Add(insured);
            return insured; // caller commits via Repository.SaveChangesAsync(), same as before
        }

        /// <summary>
        /// Self-service create (individual or corporate). Password hashing
        /// is intentionally out of scope for this pass, per your call -
        /// Password is not set here, matching the previous behavior (this
        /// DTO's Password is currently write-only/unused, same as before).
        /// </summary>
        public async Task<InsuredClients> CustomerCreate(CreateNewCustomerRequest newCustomerDto)
        {
            if (newCustomerDto == null) throw new ArgumentNullException(nameof(newCustomerDto));

            var fullName = $"{newCustomerDto.FirstName} {newCustomerDto.LastName}".Trim();

            // FIX (this pass): for a CORPORATE registration, FullName below
            // gets swapped out for the org's name inside the helper, so
            // there's no room left to compose "FirstName LastName" there.
            // The contact's FirstName was being lost entirely - OtherNames
            // was set from the DTO's separate OtherName field instead, and
            // FirstName never landed anywhere, so it always came back blank
            // on read. It's now stored directly in OtherNames for a
            // corporate contact (SurName still holds LastName).
            //
            // Real trade-off, not swept under the rug: the DTO's OtherName
            // field (a distinct middle/other-name value) has no slot left
            // for a corporate contact and is dropped - it still works
            // normally for individuals, where it's embedded in OtherNames
            // untouched and FirstName is recovered from FullName instead.
            var contactOtherNames = newCustomerDto.IsOrg ? newCustomerDto.FirstName : newCustomerDto.OtherName;

            return await BuildAndCheckDuplicateInsuredAsync(
                surName: newCustomerDto.LastName, otherNames: contactOtherNames, fullName: fullName,
                title: newCustomerDto.Title,
                address: newCustomerDto.Address, cityLGA: newCustomerDto.CityLGA, state: newCustomerDto.StateID,
                landphone: newCustomerDto.PhoneLine2, mobilePhone: newCustomerDto.PhoneLine1,
                email: newCustomerDto.Email, nationalId: null, occupation: null, gender: newCustomerDto.Gender?.ToString(),
                maritalStatus: null, country: newCustomerDto.Nationality, dateOfBirth: newCustomerDto.DateOfBirth,
                kycNumber: newCustomerDto.KycNumber, nextOfKin: newCustomerDto.NextOfKin,
                isCorporate: newCustomerDto.IsOrg, orgName: newCustomerDto.OrgName,
                orgRegNumber: newCustomerDto.OrgRegNumber, orgRegDate: newCustomerDto.OrgRegDate);
        }

        /// <summary>Admin/agent create (individual or corporate). No password on this path, same as before.</summary>
        public async Task<InsuredClients> CustomerCreateMaster(CreateNewCustomerRequestMaster newCustomerDto)
        {
            if (newCustomerDto == null) throw new ArgumentNullException(nameof(newCustomerDto));

            return await BuildAndCheckDuplicateInsuredAsync(
                surName: newCustomerDto.Surname, otherNames: newCustomerDto.OtherNames, fullName: newCustomerDto.FullName,
                title: null,
                address: newCustomerDto.Address, cityLGA: newCustomerDto.CityLGA, state: newCustomerDto.StateID,
                landphone: newCustomerDto.LandPhone, mobilePhone: newCustomerDto.MobilePhone,
                email: newCustomerDto.Email, nationalId: newCustomerDto.NationalID, occupation: newCustomerDto.Occupation,
                gender: null,
                maritalStatus: null, country: newCustomerDto.Nationality, dateOfBirth: newCustomerDto.DateOfBirth,
                kycNumber: newCustomerDto.KycNumber, nextOfKin: newCustomerDto.NextOfKin,
                isCorporate: newCustomerDto.IsOrg, orgName: newCustomerDto.OrgName,
                orgRegNumber: newCustomerDto.OrgRegNumber, orgRegDate: newCustomerDto.OrgRegDate);
        }

        /// <summary>Partial update of a customer's own contact/profile fields. Unchanged - works identically for both types.</summary>
        public void ApplyUpdate(InsuredClients customer, UpdateCustomerRequest request)
        {
            if (request.Address != null) customer.Address = request.Address;
            if (request.Email != null) customer.Email = request.Email;
            if (request.MobilePhone != null) customer.MobilePhone = request.MobilePhone;
            if (request.Landphone != null) customer.Landphone = request.Landphone;
            if (request.OtherNames != null) customer.OtherNames = request.OtherNames;

            customer.ModifiedOn = DateTime.Now;
            customer.ModifiedBy = _authContext.User?.AppId;
        }

        // -----------------------------------------------------------
        // Get-or-add wrappers - unchanged, still just delegate to
        // CustomerCreate/CustomerCreateMaster above.
        // -----------------------------------------------------------

        public async Task<InsuredClients> CustomerGetOrAddAsync(Policiesdto newpolicydto)
        {
            if (!string.IsNullOrWhiteSpace(newpolicydto.CustomerID))
            {
                var insured = await CustomerSelectThisAsync(newpolicydto.CustomerID);
                if (insured != null) return insured;
            }

            if (newpolicydto.Insured != null)
                return await CustomerCreate(newpolicydto.Insured);

            throw new InvalidOperationException(
                "Either specify CustomerID for an existing customer, or populate Insured to create a new one.");
        }

        public async Task<InsuredClients> CustomerMasterGetOrAddAsync(PolicyModel newpolicydto)
        {
            if (!string.IsNullOrWhiteSpace(newpolicydto.CustomerID))
            {
                var insured = await CustomerSelectThisAsync(newpolicydto.CustomerID);
                if (insured != null) return insured;
            }

            if (newpolicydto.Insured != null)
                return await CustomerCreateMaster(newpolicydto.Insured);

            throw new InvalidOperationException(
                "Either specify CustomerID for an existing customer, or populate Insured to create a new one.");
        }
    }
}