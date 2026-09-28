using GibsLifesMicroWebApi.Models.Customer;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Contracts.V1
{
    /// <summary>
    /// Admin/agent-created customer (no login credentials set at creation -
    /// see CreateNewCustomerRequest for the self-service path, which does
    /// collect a Password).
    ///
    /// FIX (this pass): this DTO had NO IsOrg/OrgName/OrgRegNumber/
    /// OrgRegDate at all - CreateNewCustomerRequest had them, but this one
    /// didn't, so a corporate customer could never be created through
    /// CustomerCreateMaster/the master path. Added here (a request-contract
    /// change, not a database one); they map onto FullName/NationalID/
    /// DateofBirth per the reuse table documented on InsuredClients.
    /// </summary>
    public class CreateNewCustomerRequestMaster : IValidatableObject
    {
        public string Surname { get; set; }

        public string FullName { get; set; }

        public string OtherNames { get; set; }

        public string Occupation { get; set; }
        public string NationalID { get; set; }

        public string Address { get; set; }

        public string MobilePhone { get; set; }

        public string LandPhone { get; set; }

        public string Email { get; set; }

        /// <summary>New: when true, this is a corporate customer. See class doc comment.</summary>
        public bool IsOrg { get; set; }
        /// <summary>Required when IsOrg is true. Maps onto FullName (see InsuredClients doc comment).</summary>
        public string OrgName { get; set; }
        /// <summary>Required when IsOrg is true. Maps onto NationalID (the CAC number, same column NIN uses for individuals).</summary>
        public string OrgRegNumber { get; set; }
        /// <summary>Optional. Maps onto DateofBirth (incorporation date) when IsOrg is true.</summary>
        public DateTime? OrgRegDate { get; set; }

        // STILL NOT PERSISTED (see InsuredClients doc comment - no existing
        // column is a clean fit, and this pass deliberately avoids a
        // migration for these specifically): KycType, KycIssueDate,
        // KycExpiryDate. KycNumber is a partial exception (rides in
        // Remarks, a pre-existing hack, unchanged). NextOfKin and CityLGA
        // ARE now persisted - see the dedicated columns on InsuredClients.
        public string CityLGA { get; set; }
        [Required]
        public string StateID { get; set; }
        [Required]
        public string Nationality { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public KycTypeEnum? KycType { get; set; }

        public string KycNumber { get; set; }

        public DateTime? KycIssueDate { get; set; }

        public DateTime? KycExpiryDate { get; set; }

        public PersonRequest NextOfKin { get; set; }

        // FIX (this pass): previously nothing on this DTO was required at
        // all besides StateID/Nationality, so a record with no name and no
        // way to reach the customer would pass validation silently. Also
        // adds the corporate-completeness check IsOrg needs.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsOrg)
            {
                if (string.IsNullOrWhiteSpace(OrgName))
                    yield return new ValidationResult(
                        "OrgName is required when IsOrg is true.", new[] { nameof(OrgName) });

                if (string.IsNullOrWhiteSpace(OrgRegNumber))
                    yield return new ValidationResult(
                        "OrgRegNumber is required when IsOrg is true.", new[] { nameof(OrgRegNumber) });
            }
            else if (string.IsNullOrWhiteSpace(FullName) && string.IsNullOrWhiteSpace(Surname))
            {
                yield return new ValidationResult(
                    "Either FullName or Surname is required for an individual customer.",
                    new[] { nameof(FullName), nameof(Surname) });
            }

            if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(MobilePhone))
            {
                yield return new ValidationResult(
                    "At least one of Email or MobilePhone is required, so the customer can be " +
                    "reached and duplicate-detection has something to match on.",
                    new[] { nameof(Email), nameof(MobilePhone) });
            }
        }
    }

    /// <summary>
    /// Self-service customer registration (collects a Password, unlike the
    /// Master/admin path above). FirstName/LastName are always required
    /// (inherited from PersonRequest) - for a corporate registration
    /// (IsOrg = true) these represent the authorized contact person, not the
    /// business itself; the business's registered name goes in OrgName.
    /// </summary>
    public class CreateNewCustomerRequest : PersonRequest, IValidatableObject
    {
        // FIX (this pass): was [Required] on a bool. [Required] on a bool is
        // a no-op for model binding - false is always "a value is present" -
        // so this never actually enforced anything and was misleading to
        // read. Corporate-completeness is now enforced properly below in
        // Validate() instead.
        public bool IsOrg { get; set; }

        /// <summary>Required when IsOrg is true. Maps onto FullName.</summary>
        public string OrgName { get; set; }
        /// <summary>Required when IsOrg is true. Maps onto NationalID (the CAC number).</summary>
        public string OrgRegNumber { get; set; }
        /// <summary>Optional. Maps onto DateofBirth (incorporation date) when IsOrg is true.</summary>
        public DateTime? OrgRegDate { get; set; }

        // FIX (this pass, by request): was [Required]. Password isn't
        // persisted or hashed anywhere in this codebase yet (see
        // InsuredRepository.CustomerCreate), so forcing callers to supply
        // one bought nothing - it's now a genuinely optional field, same as
        // everything else that isn't actually enforced downstream.
        public string Password { get; set; }

        // FIX (this pass): this class used to redeclare
        //   public string Email { get; set; } = string.Empty;
        // which HID PersonRequest's [Required, EmailAddress] Email rather
        // than overriding it - property hiding via `new`-less shadowing,
        // not polymorphism. ASP.NET's model binder/validator operates on
        // the most-derived declared property, so every customer created
        // through this DTO had Email validation silently skipped regardless
        // of PersonRequest's attribute - a request with Email omitted or
        // garbage would bind Email to "" (or whatever was sent) and pass
        // ModelState.IsValid every time. Deleted; this class now inherits
        // the validated Email as intended.

        [Required]
        public string CityLGA { get; set; }
        [Required]
        public string StateID { get; set; }
        [Required]
        public string Nationality { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public KycTypeEnum? KycType { get; set; }

        public string KycNumber { get; set; }

        public DateTime? KycIssueDate { get; set; }

        public DateTime? KycExpiryDate { get; set; }

        public PersonRequest NextOfKin { get; set; }

        // FIX (this pass): PersonRequest's fields no longer carry [Required]
        // (see its doc comment), so this now explicitly owns requiredness -
        // and does it CONDITIONALLY, which attributes alone can't express:
        // an organisation needs its own identity (OrgName/OrgRegNumber) and
        // a way to be reached, but no personal/contact-person data at all;
        // an individual still needs name + reachable contact info, same as
        // attribute-based validation enforced before.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsOrg)
            {
                if (string.IsNullOrWhiteSpace(OrgName))
                    yield return new ValidationResult(
                        "OrgName is required when IsOrg is true.", new[] { nameof(OrgName) });

                if (string.IsNullOrWhiteSpace(OrgRegNumber))
                    yield return new ValidationResult(
                        "OrgRegNumber is required when IsOrg is true (this becomes the customer's " +
                        "NationalID, the same column NIN uses for individual customers).",
                        new[] { nameof(OrgRegNumber) });

                if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(PhoneLine1))
                    yield return new ValidationResult(
                        "At least one of Email or PhoneLine1 is required, so the organisation can be reached. " +
                        "No personal/contact-person fields (FirstName, LastName, etc.) are required for an organisation.",
                        new[] { nameof(Email), nameof(PhoneLine1) });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(FirstName))
                    yield return new ValidationResult("FirstName is required.", new[] { nameof(FirstName) });

                if (string.IsNullOrWhiteSpace(LastName))
                    yield return new ValidationResult("LastName is required.", new[] { nameof(LastName) });

                if (string.IsNullOrWhiteSpace(Email))
                    yield return new ValidationResult("Email is required.", new[] { nameof(Email) });

                if (string.IsNullOrWhiteSpace(Address))
                    yield return new ValidationResult("Address is required.", new[] { nameof(Address) });

                if (string.IsNullOrWhiteSpace(PhoneLine1))
                    yield return new ValidationResult("PhoneLine1 is required.", new[] { nameof(PhoneLine1) });
            }
        }
    }

    public class CustomerResult : CreateNewCustomerRequest
    {
        public CustomerResult(InsuredClients client)
        {
            CustomerID = client.InsuredID;

            // FIX (this pass): IsOrg now reads the real discriminator
            // (client.IsCorporate, backed by ClientType via ClientTypes)
            // instead of the old heuristic `client.Occupation == "CORPORATE"`,
            // which meant every customer created through this controller
            // came back non-corporate regardless of what was submitted -
            // Occupation is never set to "CORPORATE" by CustomerCreate at
            // all, only by the unrelated CreateOrGetAssuredCodeAsync path.
            IsOrg = client.IsCorporate;

            if (IsOrg)
            {
                // FIX: these were commented out with no column to read from
                // (OrgRegNumber used to read client.KycIdNumber, a member
                // that doesn't even exist on InsuredClients). Now backed by
                // the real reuse mapping - see InsuredClients doc comment.
                OrgName = client.FullName;
                OrgRegNumber = client.NationalID;
                OrgRegDate = client.DateofBirth;

                // Contact person lives directly in SurName/OtherNames for a
                // corporate record (FullName holds the org's name instead),
                // so the FullName-minus-SurName extraction below doesn't
                // apply here. OtherNames holds the contact's FirstName (see
                // the matching FIX comment in InsuredRepository.CustomerCreate) -
                // NOT the DTO's separate OtherName field, which has no slot
                // left for a corporate contact and doesn't round-trip.
                FirstName = client.OtherNames;
                LastName = client.SurName;
                OtherName = null;
            }
            else
            {
                // FIX: was `FirstName = client.FullName;` - InsuredClients
                // has no dedicated FirstName column, only FullName and
                // SurName, so this used to return the ENTIRE full name as
                // "firstName". Now strips the surname back off, since
                // FullName is composed as "First Last" for individuals.
                FirstName = ExtractFirstName(client.FullName, client.SurName);
                LastName = client.SurName;
                OtherName = client.OtherNames;
            }

            Email = client.Email;
            Address = client.Address;
            // FIX (this pass): was hardcoded null below - CityLGA now has a
            // real column (see InsuredClients), so it round-trips like
            // everything else instead of always coming back empty.
            CityLGA = client.CityLGA;
            PhoneLine2 = client.Landphone;
            PhoneLine1 = client.MobilePhone;

            Title = client.Title;
            Gender = ParseGender(client.Sex);
            DateOfBirth = client.DateofBirth;
            Nationality = client.Country;
            StateID = client.State;

            // Never return the password, regardless of whether it's ever
            // persisted. (Password hashing itself is intentionally out of
            // scope for this pass.)
            Password = null;

            // STILL NULL - no existing column is a clean, unambiguous fit
            // and this pass deliberately avoids a schema migration for
            // these specifically (see InsuredClients doc comment). CityLGA
            // is NOT in this list anymore - it's set above from the real
            // column.
            KycType = null;
            KycIssueDate = null;
            KycExpiryDate = null;

            // NEW (this pass): reads from the flat NextOfKin* columns on
            // InsuredClients (see that class's doc comment) rather than
            // being hardcoded null. No Include() needed - these are plain
            // columns on the same row, always loaded with the entity.
            NextOfKin = !client.HasNextOfKin ? null : new PersonRequest
            {
                Title = client.NextOfKinTitle,
                FirstName = client.NextOfKinFirstName,
                LastName = client.NextOfKinLastName,
                OtherName = client.NextOfKinOtherName,
                Gender = ParseGender(client.NextOfKinGender),
                Email = client.NextOfKinEmail,
                Address = client.NextOfKinAddress,
                PhoneLine1 = client.NextOfKinPhoneLine1,
                PhoneLine2 = client.NextOfKinPhoneLine2,
            };
            // KycNumber is the one partial exception: CustomerCreate/
            // CustomerCreateMaster stash it in the Remarks column (a
            // pre-existing hack, not introduced here), so it's readable
            // back, but Remarks isn't a dedicated field and could get
            // overwritten by anything else that writes to Remarks later.
            KycNumber = client.Remarks;
        }

        public CustomerResult(Models.Policy policy)
        {
            CustomerID = policy.InsuredID;

            // NOTE (pre-existing, unchanged by this pass): Policy has no
            // ClientTypes-style discriminator visible from here, so this
            // constructor keeps the original Occupation heuristic. If
            // corporate policies need reliable IsOrg reporting, that needs
            // the same treatment applied to wherever Policy is populated -
            // out of scope here since that model/flow isn't part of this
            // customer-endpoint pass.
            IsOrg = policy.InsOccupation == "CORPORATE";

            Password = null;
            Title = null;

            LastName = policy.InsSurname;
            FirstName = policy.InsFirstname;
            OtherName = policy.InsOthernames;
            Address = policy.InsAddress;
            PhoneLine1 = policy.InsMobilePhone;
            PhoneLine2 = policy.InsLandPhone;
            Email = policy.InsEmail;
            StateID = policy.InsStateID;

            CityLGA = null;
            Nationality = null;
            KycType = null;
            KycNumber = null;
        }

        public string CustomerID { get; set; }

        /// <summary>
        /// Recovers a first name from a combined "First Last" FullName by
        /// stripping the known SurName off the end. This only works because
        /// InsuredRepository always composes FullName as
        /// $"{FirstName} {LastName}".Trim() on create for individuals - if
        /// that composition rule ever changes, this needs to change with it.
        /// Falls back to returning FullName unchanged when SurName is blank
        /// or doesn't match the end of FullName (e.g. customers created via
        /// CustomerCreateMaster, whose FullName isn't necessarily composed
        /// this way), which is no worse than the previous behavior.
        /// </summary>
        private static string ExtractFirstName(string fullName, string surName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return fullName;

            if (!string.IsNullOrWhiteSpace(surName)
                && fullName.EndsWith(surName, StringComparison.OrdinalIgnoreCase))
            {
                var withoutSurname = fullName.Substring(0, fullName.Length - surName.Length).TrimEnd();
                if (!string.IsNullOrWhiteSpace(withoutSurname))
                    return withoutSurname;
            }

            return fullName;
        }

        private static GenderEnum? ParseGender(string sex)
        {
            if (string.IsNullOrWhiteSpace(sex)) return null;
            return Enum.TryParse<GenderEnum>(sex, ignoreCase: true, out var parsed) ? parsed : (GenderEnum?)null;
        }
    }

    /// <summary>
    /// Shared shape for a person - used as the base of CreateNewCustomerRequest
    /// (the contact person on an individual signup) AND as the type of the
    /// NextOfKin field on both create DTOs.
    ///
    /// FIX (this pass): every field here used to be [Required]. That was
    /// wrong on two counts: (1) CreateNewCustomerRequest inherits this, so a
    /// CORPORATE signup (IsOrg=true) was forced to supply a contact person's
    /// full name/email/address/phone even though an organisation doesn't
    /// have "personal data" to give - by request, this pass removes that
    /// requirement entirely for organisations (see CreateNewCustomerRequest.
    /// Validate(), which now owns all the individual-only requiredness that
    /// used to live here as attributes); (2) NextOfKin is also typed as
    /// PersonRequest, so a partially-known next of kin (e.g. just a name and
    /// phone number) could never be submitted at all. Format validation
    /// ([EmailAddress]) stays - an email that IS provided still has to look
    /// like one - only the "must be present" requirement moved out.
    /// </summary>
    public class PersonRequest
    {
        public string Title { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }

        public string OtherName { get; set; }

        public GenderEnum? Gender { get; set; }
        [EmailAddress]
        public string Email { get; set; }
        public string Address { get; set; }
        public string PhoneLine1 { get; set; }

        public string PhoneLine2 { get; set; }
    }


    public class BulkUploadErrorDto
    {
        public long ID { get; set; }
        public string? UploadBatchID { get; set; }
        public string? PolicyNo { get; set; }
        public string? MemberNo { get; set; }
        public string? MemberName { get; set; }
        public string? ErrorReason { get; set; }
        public string? RawData { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime? UploadedOn { get; set; }
        public string? IsResolved { get; set; }
        public DateTime? ResolvedOn { get; set; }
        public string? ResolvedBy { get; set; }
    }

    public class BulkUploadErrorCreateDto
    {
        public string? UploadBatchID { get; set; }
        public string? PolicyNo { get; set; }
        public string? MemberNo { get; set; }
        public string? MemberName { get; set; }
        public string? ErrorReason { get; set; }
        public string? RawData { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime? UploadedOn { get; set; }
        public string? IsResolved { get; set; } = "N";
    }
    public class BulkUploadErrorUpdateDto
    {
        public long ID { get; set; }
        public string? MemberNo { get; set; }
        public string? MemberName { get; set; }
        public string? ErrorReason { get; set; }
        public string? RawData { get; set; }
        public string? IsResolved { get; set; }
        public DateTime? ResolvedOn { get; set; }
        public string? ResolvedBy { get; set; }
    }

    public class BulkUploadErrorQueryDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? UploadBatchID { get; set; }
        public string? PolicyNo { get; set; }
        public string? MemberNo { get; set; }
        public string? MemberName { get; set; }
        public string? UploadedBy { get; set; }
        public string? IsResolved { get; set; }
    }
}